using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using MyClock.Models;

namespace MyClock.Services;

public sealed class SqliteFocusSessionRepository : IFocusSessionRepository
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    private bool _isInitialized;

    public SqliteFocusSessionRepository(string databasePath)
    {
        _databasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    // %LOCALAPPDATA%\MyClock\focus.db
    public static string DefaultDatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyClock",
        "focus.db");

    public long Add(FocusSession session)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO focus_sessions (window_title, process_name, started_at, ended_at, monitored_ms, foreground_ms, switch_count)
            VALUES ($windowTitle, $processName, $startedAt, $endedAt, $monitoredMs, $foregroundMs, $switchCount);
            SELECT last_insert_rowid();
            """;
        AddValueParameters(command, session);
        return (long)command.ExecuteScalar()!;
    }

    public void Update(FocusSession session)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE focus_sessions
            SET window_title = $windowTitle,
                process_name = $processName,
                started_at = $startedAt,
                ended_at = $endedAt,
                monitored_ms = $monitoredMs,
                foreground_ms = $foregroundMs,
                switch_count = $switchCount
            WHERE id = $id;
            """;
        AddValueParameters(command, session);
        command.Parameters.AddWithValue("$id", session.Id);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<FocusSession> GetSessionsStartedBetween(DateTimeOffset from, DateTimeOffset to)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, window_title, process_name, started_at, ended_at, monitored_ms, foreground_ms, switch_count
            FROM focus_sessions
            WHERE started_at >= $from AND started_at < $to
            ORDER BY started_at;
            """;
        command.Parameters.AddWithValue("$from", FormatTimestamp(from));
        command.Parameters.AddWithValue("$to", FormatTimestamp(to));

        var sessions = new List<FocusSession>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            sessions.Add(new FocusSession
            {
                Id = reader.GetInt64(0),
                WindowTitle = reader.GetString(1),
                ProcessName = reader.IsDBNull(2) ? null : reader.GetString(2),
                StartedAt = ParseTimestamp(reader.GetString(3)),
                EndedAt = reader.IsDBNull(4) ? null : ParseTimestamp(reader.GetString(4)),
                MonitoredDuration = TimeSpan.FromMilliseconds(reader.GetInt64(5)),
                ForegroundDuration = TimeSpan.FromMilliseconds(reader.GetInt64(6)),
                SwitchCount = reader.GetInt32(7)
            });
        }

        return sessions;
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
            InitializeSchema(connection);
            _isInitialized = true;
        }

        return connection;
    }

    private static void InitializeSchema(SqliteConnection connection)
    {
        // user_version records the schema version so later changes can migrate existing files.
        using var command = connection.CreateCommand();
        command.CommandText = """
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
            PRAGMA user_version = 1;
            """;
        command.ExecuteNonQuery();
    }

    private static void AddValueParameters(SqliteCommand command, FocusSession session)
    {
        command.Parameters.AddWithValue("$windowTitle", session.WindowTitle);
        command.Parameters.AddWithValue("$processName", (object?)session.ProcessName ?? DBNull.Value);
        command.Parameters.AddWithValue("$startedAt", FormatTimestamp(session.StartedAt));
        command.Parameters.AddWithValue("$endedAt", session.EndedAt is { } endedAt ? FormatTimestamp(endedAt) : DBNull.Value);
        command.Parameters.AddWithValue("$monitoredMs", (long)session.MonitoredDuration.TotalMilliseconds);
        command.Parameters.AddWithValue("$foregroundMs", (long)session.ForegroundDuration.TotalMilliseconds);
        command.Parameters.AddWithValue("$switchCount", session.SwitchCount);
    }

    // Timestamps are stored as round-trip UTC text so they sort correctly as strings.
    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime();
}
