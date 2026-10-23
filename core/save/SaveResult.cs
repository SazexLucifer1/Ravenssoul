using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Utopia;

/// <summary>
/// Outcome of a save-service call. Carries a developer detail (logs only) and a
/// translation key for the player-facing explanation.
/// </summary>
public partial class SaveResult : Godot.RefCounted
{
    public enum Status { Ok, NotFound, Corrupted, VersionTooNew, ReadFailed, WriteFailed, InvalidSlot }

    public static readonly IReadOnlyDictionary<Status, string> PlayerMessageKeys = new Dictionary<Status, string>
    {
        [Status.Ok] = "SAVE_STATUS_OK",
        [Status.NotFound] = "SAVE_ERROR_NOT_FOUND",
        [Status.Corrupted] = "SAVE_ERROR_CORRUPTED",
        [Status.VersionTooNew] = "SAVE_ERROR_VERSION_TOO_NEW",
        [Status.ReadFailed] = "SAVE_ERROR_READ_FAILED",
        [Status.WriteFailed] = "SAVE_ERROR_WRITE_FAILED",
        [Status.InvalidSlot] = "SAVE_ERROR_INVALID_SLOT",
    };

    public Status State { get; private set; } = Status.Ok;

    /// <summary>The game payload (only set on successful loads).</summary>
    public JsonObject Data { get; private set; } = new();

    /// <summary>Developer-only detail. Never display it.</summary>
    public string Detail { get; private set; } = "";

    public static SaveResult Success(JsonObject payload = null) => new() { Data = payload ?? new JsonObject() };

    public static SaveResult Failure(Status status, string detail) => new() { State = status, Detail = detail };

    public bool IsOk() => State == Status.Ok;

    /// <summary>Translation key explaining the outcome to a player in plain language.</summary>
    public string PlayerMessageKey() => PlayerMessageKeys[State];
}
