using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using MyClock.Models;

namespace MyClock.Services;

public sealed class SqliteFocusSessionRepository : IFocusSessionRepository
{
    // Version 1: focus_sessions. Version 2: focus_switches and focus_switch_visits.
    private const int SchemaVersion = 2;

    private readonly string _databasePath;
    private readonly string _connectionString;
    private bool _isInitialized;

    public SqliteFocusSessionRepository(string databasePath)
    {
        _databasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();
    }

    public string DatabasePath => _databasePath;

    // %LOCALAPPDATA%\MyClock\focus.db
    public static string DefaultDatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyClock",
        "focus.db");

    public void Save(FocusSession session, IEnumerable<FocusSwitch> changedSwitches)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        // Ids of inserted rows are applied only after commit.
        var assignIds = new List<Action>();

        var sessionId = session.Id;
        if (sessionId == 0)
        {
            sessionId = InsertSession(connection, transaction, session);
            var insertedId = sessionId;
            assignIds.Add(() => session.Id = insertedId);
        }
        else
        {
            UpdateSession(connection, transaction, session);
        }

        foreach (var focusSwitch in changedSwitches)
        {
            var switchId = focusSwitch.Id;
            if (switchId == 0)
            {
                switchId = InsertSwitch(connection, transaction, sessionId, focusSwitch);
                var insertedId = switchId;
                assignIds.Add(() => focusSwitch.Id = insertedId);
            }
            else
            {
                UpdateSwitch(connection, transaction, focusSwitch);
            }

            foreach (var visit in focusSwitch.Visits)
            {
                if (visit.Id == 0)
                {
                    var insertedId = InsertVisit(connection, transaction, switchId, visit);
                    assignIds.Add(() => visit.Id = insertedId);
                }
                else
                {
                    UpdateVisit(connection, transaction, visit);
                }
            }
        }

        transaction.Commit();
        foreach (var assignId in assignIds)
        {
            assignId();
        }
    }

    public IReadOnlyList<FocusSession> GetSessionsStartedBetween(DateTimeOffset from, DateTimeOffset to)
    {
        using var connection = OpenConnection();
        var fromText = FormatTimestamp(from);
        var toText = FormatTimestamp(to);

        var sessions = new List<FocusSession>();
        var sessionsById = new Dictionary<long, FocusSession>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT id, window_title, process_name, started_at, ended_at, monitored_ms, foreground_ms, switch_count
                FROM focus_sessions
                WHERE started_at >= $from AND started_at < $to
                ORDER BY started_at, id;
                """;
            command.Parameters.AddWithValue("$from", fromText);
            command.Parameters.AddWithValue("$to", toText);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var session = new FocusSession
                {
                    Id = reader.GetInt64(0),
                    WindowTitle = reader.GetString(1),
                    ProcessName = reader.IsDBNull(2) ? null : reader.GetString(2),
                    StartedAt = ParseTimestamp(reader.GetString(3)),
                    EndedAt = reader.IsDBNull(4) ? null : ParseTimestamp(reader.GetString(4)),
                    MonitoredDuration = TimeSpan.FromMilliseconds(reader.GetInt64(5)),
                    ForegroundDuration = TimeSpan.FromMilliseconds(reader.GetInt64(6)),
                    SwitchCount = reader.GetInt32(7)
                };
                sessions.Add(session);
                sessionsById[session.Id] = session;
            }
        }

        var switchesById = new Dictionary<long, FocusSwitch>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT w.id, w.session_id, w.left_at, w.returned_at, w.away_ms
                FROM focus_switches w
                JOIN focus_sessions s ON s.id = w.session_id
                WHERE s.started_at >= $from AND s.started_at < $to
                ORDER BY w.left_at, w.id;
                """;
            command.Parameters.AddWithValue("$from", fromText);
            command.Parameters.AddWithValue("$to", toText);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var focusSwitch = new FocusSwitch
                {
                    Id = reader.GetInt64(0),
                    LeftAt = ParseTimestamp(reader.GetString(2)),
                    ReturnedAt = reader.IsDBNull(3) ? null : ParseTimestamp(reader.GetString(3)),
                    AwayDuration = TimeSpan.FromMilliseconds(reader.GetInt64(4))
                };
                sessionsById[reader.GetInt64(1)].Switches.Add(focusSwitch);
                switchesById[focusSwitch.Id] = focusSwitch;
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT v.id, v.switch_id, v.window_title, v.process_name, v.started_at, v.duration_ms
                FROM focus_switch_visits v
                JOIN focus_switches w ON w.id = v.switch_id
                JOIN focus_sessions s ON s.id = w.session_id
                WHERE s.started_at >= $from AND s.started_at < $to
                ORDER BY v.started_at, v.id;
                """;
            command.Parameters.AddWithValue("$from", fromText);
            command.Parameters.AddWithValue("$to", toText);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                switchesById[reader.GetInt64(1)].Visits.Add(new FocusSwitchVisit
                {
                    Id = reader.GetInt64(0),
                    WindowTitle = reader.GetString(2),
                    ProcessName = reader.IsDBNull(3) ? null : reader.GetString(3),
                    StartedAt = ParseTimestamp(reader.GetString(4)),
                    Duration = TimeSpan.FromMilliseconds(reader.GetInt64(5))
                });
            }
        }

        return sessions;
    }

    // Switches and visits are removed by ON DELETE CASCADE (foreign keys are on).
    public int DeleteSessionsStartedBefore(DateTimeOffset cutoff, long? exceptSessionId = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM focus_sessions WHERE started_at < $cutoff AND id IS NOT $exceptId;";
        command.Parameters.AddWithValue("$cutoff", FormatTimestamp(cutoff));
        command.Parameters.AddWithValue("$exceptId", (object?)exceptSessionId ?? DBNull.Value);
        return command.ExecuteNonQuery();
    }

    public int DeleteAllSessions(long? exceptSessionId = null)
    {
        using var connection = OpenConnection();
        int deleted;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM focus_sessions WHERE id IS NOT $exceptId;";
            command.Parameters.AddWithValue("$exceptId", (object?)exceptSessionId ?? DBNull.Value);
            deleted = command.ExecuteNonQuery();
        }

        // Give the freed space back to the file system.
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "VACUUM;";
            command.ExecuteNonQuery();
        }

        return deleted;
    }

    private static long InsertSession(SqliteConnection connection, SqliteTransaction transaction, FocusSession session)
    {
        using var command = CreateCommand(connection, transaction, """
            INSERT INTO focus_sessions (window_title, process_name, started_at, ended_at, monitored_ms, foreground_ms, switch_count)
            VALUES ($windowTitle, $processName, $startedAt, $endedAt, $monitoredMs, $foregroundMs, $switchCount);
            SELECT last_insert_rowid();
            """);
        AddSessionParameters(command, session);
        return (long)command.ExecuteScalar()!;
    }

    private static void UpdateSession(SqliteConnection connection, SqliteTransaction transaction, FocusSession session)
    {
        using var command = CreateCommand(connection, transaction, """
            UPDATE focus_sessions
            SET window_title = $windowTitle,
                process_name = $processName,
                started_at = $startedAt,
                ended_at = $endedAt,
                monitored_ms = $monitoredMs,
                foreground_ms = $foregroundMs,
                switch_count = $switchCount
            WHERE id = $id;
            """);
        AddSessionParameters(command, session);
        command.Parameters.AddWithValue("$id", session.Id);
        command.ExecuteNonQuery();
    }

    private static long InsertSwitch(SqliteConnection connection, SqliteTransaction transaction, long sessionId, FocusSwitch focusSwitch)
    {
        using var command = CreateCommand(connection, transaction, """
            INSERT INTO focus_switches (session_id, left_at, returned_at, away_ms)
            VALUES ($sessionId, $leftAt, $returnedAt, $awayMs);
            SELECT last_insert_rowid();
            """);
        command.Parameters.AddWithValue("$sessionId", sessionId);
        AddSwitchParameters(command, focusSwitch);
        return (long)command.ExecuteScalar()!;
    }

    private static void UpdateSwitch(SqliteConnection connection, SqliteTransaction transaction, FocusSwitch focusSwitch)
    {
        using var command = CreateCommand(connection, transaction, """
            UPDATE focus_switches
            SET left_at = $leftAt,
                returned_at = $returnedAt,
                away_ms = $awayMs
            WHERE id = $id;
            """);
        AddSwitchParameters(command, focusSwitch);
        command.Parameters.AddWithValue("$id", focusSwitch.Id);
        command.ExecuteNonQuery();
    }

    private static long InsertVisit(SqliteConnection connection, SqliteTransaction transaction, long switchId, FocusSwitchVisit visit)
    {
        using var command = CreateCommand(connection, transaction, """
            INSERT INTO focus_switch_visits (switch_id, window_title, process_name, started_at, duration_ms)
            VALUES ($switchId, $windowTitle, $processName, $startedAt, $durationMs);
            SELECT last_insert_rowid();
            """);
        command.Parameters.AddWithValue("$switchId", switchId);
        AddVisitParameters(command, visit);
        return (long)command.ExecuteScalar()!;
    }

    private static void UpdateVisit(SqliteConnection connection, SqliteTransaction transaction, FocusSwitchVisit visit)
    {
        // Only the duration changes after a visit is inserted.
        using var command = CreateCommand(connection, transaction, """
            UPDATE focus_switch_visits
            SET duration_ms = $durationMs
            WHERE id = $id;
            """);
        command.Parameters.AddWithValue("$durationMs", ToMilliseconds(visit.Duration));
        command.Parameters.AddWithValue("$id", visit.Id);
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        if (!_isInitialized)
        {
            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        if (!_isInitialized)
        {
            MigrateSchema(connection);
            _isInitialized = true;
        }

        return connection;
    }

    // Brings a new or older database file up to SchemaVersion. Each step runs only
    // once per file; PRAGMA user_version stores the version the file has reached.
    private static void MigrateSchema(SqliteConnection connection)
    {
        long version;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA user_version;";
            version = (long)command.ExecuteScalar()!;
        }

        if (version >= SchemaVersion)
        {
            return;
        }

        using var transaction = connection.BeginTransaction();

        if (version < 1)
        {
            ExecuteNonQuery(connection, transaction, """
                CREATE TABLE IF NOT EXISTS focus_sessions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    window_title TEXT NOT NULL,
                    process_name TEXT,
                    started_at TEXT NOT NULL,
                    ended_at TEXT,
                    monitored_ms INTEGER NOT NULL DEFAULT 0,
                    foreground_ms INTEGER NOT NULL DEFAULT 0,
                    switch_count INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS ix_focus_sessions_started_at ON focus_sessions (started_at);
                """);
        }

        if (version < 2)
        {
            ExecuteNonQuery(connection, transaction, """
                CREATE TABLE IF NOT EXISTS focus_switches (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    session_id INTEGER NOT NULL REFERENCES focus_sessions (id) ON DELETE CASCADE,
                    left_at TEXT NOT NULL,
                    returned_at TEXT,
                    away_ms INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS ix_focus_switches_session_id ON focus_switches (session_id);
                CREATE TABLE IF NOT EXISTS focus_switch_visits (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    switch_id INTEGER NOT NULL REFERENCES focus_switches (id) ON DELETE CASCADE,
                    window_title TEXT NOT NULL,
                    process_name TEXT,
                    started_at TEXT NOT NULL,
                    duration_ms INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS ix_focus_switch_visits_switch_id ON focus_switch_visits (switch_id);
                """);
        }

        // PRAGMA does not accept parameters; SchemaVersion is a constant.
        ExecuteNonQuery(connection, transaction, $"PRAGMA user_version = {SchemaVersion};");
        transaction.Commit();
    }

    private static SqliteCommand CreateCommand(SqliteConnection connection, SqliteTransaction transaction, string commandText)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        return command;
    }

    private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string commandText)
    {
        using var command = CreateCommand(connection, transaction, commandText);
        command.ExecuteNonQuery();
    }

    private static void AddSessionParameters(SqliteCommand command, FocusSession session)
    {
        command.Parameters.AddWithValue("$windowTitle", session.WindowTitle);
        command.Parameters.AddWithValue("$processName", (object?)session.ProcessName ?? DBNull.Value);
        command.Parameters.AddWithValue("$startedAt", FormatTimestamp(session.StartedAt));
        command.Parameters.AddWithValue("$endedAt", FormatNullableTimestamp(session.EndedAt));
        command.Parameters.AddWithValue("$monitoredMs", ToMilliseconds(session.MonitoredDuration));
        command.Parameters.AddWithValue("$foregroundMs", ToMilliseconds(session.ForegroundDuration));
        command.Parameters.AddWithValue("$switchCount", session.SwitchCount);
    }

    private static void AddSwitchParameters(SqliteCommand command, FocusSwitch focusSwitch)
    {
        command.Parameters.AddWithValue("$leftAt", FormatTimestamp(focusSwitch.LeftAt));
        command.Parameters.AddWithValue("$returnedAt", FormatNullableTimestamp(focusSwitch.ReturnedAt));
        command.Parameters.AddWithValue("$awayMs", ToMilliseconds(focusSwitch.AwayDuration));
    }

    private static void AddVisitParameters(SqliteCommand command, FocusSwitchVisit visit)
    {
        command.Parameters.AddWithValue("$windowTitle", visit.WindowTitle);
        command.Parameters.AddWithValue("$processName", (object?)visit.ProcessName ?? DBNull.Value);
        command.Parameters.AddWithValue("$startedAt", FormatTimestamp(visit.StartedAt));
        command.Parameters.AddWithValue("$durationMs", ToMilliseconds(visit.Duration));
    }

    private static long ToMilliseconds(TimeSpan duration) => (long)duration.TotalMilliseconds;

    // Timestamps are stored as round-trip UTC text so they sort correctly as strings.
    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static object FormatNullableTimestamp(DateTimeOffset? value) =>
        value is { } timestamp ? FormatTimestamp(timestamp) : DBNull.Value;

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime();
}
