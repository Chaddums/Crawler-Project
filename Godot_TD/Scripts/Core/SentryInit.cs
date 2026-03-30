using System;
using Godot;
using Sentry;

namespace JunkyardTD
{
    /// <summary>
    /// Autoload that initializes Sentry error tracking on game startup.
    /// Must be loaded before GameManager in project.godot autoloads.
    /// </summary>
    public partial class SentryInit : Node
    {
        private const string DSN = "https://2643a60417f270ba5c2e3852755ebce0@o4511131557036032.ingest.us.sentry.io/4511131742240768";

        private static IDisposable _sentryDisposable;

        public override void _Ready()
        {
            if (_sentryDisposable != null)
                return; // Already initialized (scene reload guard)

            try
            {
                _sentryDisposable = SentrySdk.Init(options =>
                {
                    options.Dsn = DSN;
                    options.Release = $"vine-logic-td@{Constants.GAME_VERSION}";
                    options.Environment = "game";
                    options.TracesSampleRate = 0.1;
                    options.IsGlobalModeEnabled = true;
                    options.AutoSessionTracking = true;

                    options.SetBeforeSend((sentryEvent, hint) =>
                    {
                        // Strip user-identifying data
                        sentryEvent.User = null;
                        sentryEvent.ServerName = null;

                        // Remove any file paths that might expose local usernames
                        if (sentryEvent.Message != null && sentryEvent.Message.Formatted != null)
                        {
                            sentryEvent.Message.Formatted = SanitizePath(sentryEvent.Message.Formatted);
                        }

                        return sentryEvent;
                    });
                });

                // Hook unhandled exceptions from the .NET runtime
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                GD.Print("[SentryInit] Sentry initialized — tracking errors for vine-logic-td@" + Constants.GAME_VERSION);
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[SentryInit] Failed to initialize Sentry: {ex.Message}");
            }
        }

        public override void _ExitTree()
        {
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        }

        public override void _Notification(int what)
        {
            // Flush events on quit so nothing is lost
            if (what == NotificationWMCloseRequest || what == NotificationCrash)
            {
                SentrySdk.Flush(TimeSpan.FromSeconds(2));
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception ex)
            {
                SentrySdk.CaptureException(ex);
                SentrySdk.Flush(TimeSpan.FromSeconds(2));
            }
        }

        /// <summary>
        /// Report an exception to Sentry manually. Call from try/catch blocks.
        /// </summary>
        public static void CaptureException(Exception ex)
        {
            SentrySdk.CaptureException(ex);
        }

        /// <summary>
        /// Send a breadcrumb for context leading up to an error.
        /// </summary>
        public static void AddBreadcrumb(string message, string category = "game")
        {
            SentrySdk.AddBreadcrumb(message, category);
        }

        private static string SanitizePath(string input)
        {
            // Replace Windows user paths (C:\Users\Username\...) with a generic placeholder
            return System.Text.RegularExpressions.Regex.Replace(
                input,
                @"[A-Za-z]:\\Users\\[^\\]+",
                "[USER_DIR]");
        }
    }
}
