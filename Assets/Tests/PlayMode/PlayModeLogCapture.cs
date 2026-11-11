using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpearWander.PlayModeTests
{
    /// <summary>
    /// Optional, non-invasive capture of Unity log messages during a test.
    /// Logs are still emitted normally; this helper only observes them.
    /// </summary>
    public sealed class PlayModeLogCapture : IDisposable
    {
        public readonly struct Entry
        {
            public Entry(LogType type, string message, string stackTrace)
            {
                Type = type;
                Message = message;
                StackTrace = stackTrace;
            }

            public LogType Type { get; }
            public string Message { get; }
            public string StackTrace { get; }
        }

        private readonly object _gate = new object();
        private readonly List<Entry> _entries = new List<Entry>();
        private bool _subscribed;

        public PlayModeLogCapture()
        {
            Application.logMessageReceived += OnLogMessageReceived;
            _subscribed = true;
        }

        public bool IsSubscribed
        {
            get
            {
                lock (_gate)
                    return _subscribed;
            }
        }

        public IReadOnlyList<Entry> Entries
        {
            get
            {
                lock (_gate)
                    return new List<Entry>(_entries);
            }
        }

        public void Clear()
        {
            lock (_gate)
                _entries.Clear();
        }

        public IReadOnlyList<Entry> Find(string messageFragment)
        {
            if (string.IsNullOrEmpty(messageFragment))
                throw new ArgumentException("A message fragment is required.", nameof(messageFragment));

            List<Entry> matches = new List<Entry>();
            lock (_gate)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    Entry entry = _entries[i];
                    if (entry.Message != null &&
                        entry.Message.IndexOf(messageFragment, StringComparison.Ordinal) >= 0)
                    {
                        matches.Add(entry);
                    }
                }
            }

            return matches;
        }

        public void Dispose()
        {
            Unsubscribe();
        }

        /// <summary>
        /// Removes the Application.logMessageReceived handler. Safe to call
        /// multiple times and safe to call from cleanup after a test failure.
        /// </summary>
        public void Unsubscribe()
        {
            lock (_gate)
            {
                if (!_subscribed)
                    return;

                _subscribed = false;
            }

            Application.logMessageReceived -= OnLogMessageReceived;
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            lock (_gate)
            {
                if (!_subscribed)
                    return;

                _entries.Add(new Entry(type, condition, stackTrace));
            }
        }
    }
}
