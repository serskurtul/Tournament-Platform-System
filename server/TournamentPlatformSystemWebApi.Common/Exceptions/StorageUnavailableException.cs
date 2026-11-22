using System;

namespace TournamentPlatformSystemWebApi.Common.Exceptions
{
    /// <summary>
    /// The banner storage dependency cannot serve the request (not configured, credentials rejected,
    /// network failure). Mapped to HTTP 503 by the API (CAPA CA-1, INC-001).
    /// </summary>
    public class StorageUnavailableException : Exception
    {
        public StorageUnavailableException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}