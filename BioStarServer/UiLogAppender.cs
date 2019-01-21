using log4net.Appender;
using log4net.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BioStarServer
{

    public class UiLogAppender : AppenderSkeleton
    {
        public event EventHandler<UiLogEventArgs> UiLogReceived;


        protected override void Append(LoggingEvent loggingEvent)
        {
            var message = RenderLoggingEvent(loggingEvent);
            OnUiLogReceived(new UiLogEventArgs(message));
        }

        protected virtual void OnUiLogReceived(UiLogEventArgs e)
        {
            UiLogReceived?.Invoke(this, e);
        }
    }

    public class UiLogEventArgs : EventArgs
    {
        public string Message { get; private set; }

        public UiLogEventArgs(string message)
        {
            Message = message;
        }
    }
    
}
