using MyClock.Models;

namespace MyClock.ViewModels;

// An entry in the comment language picker.
public sealed record LanguageOption(CommentLanguage Value, string Name);
