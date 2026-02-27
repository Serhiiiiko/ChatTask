using System.ComponentModel.DataAnnotations;

namespace DebtChat.Core.Configuration;

/// <summary>
/// Configuration options for the Claude LLM provider (Anthropic API).
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "LLM";

    /// <summary>
    /// The model name to use (e.g., "claude-sonnet-4-6").
    /// </summary>
    [Required]
    public required string Model { get; set; }
}
