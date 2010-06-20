using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Blogger2BlogML
{
    /// <summary>
    /// A message to display or log
    /// </summary>
    public class ConverterMessageEventArgs : EventArgs
    {
        /// <summary>
        /// The message
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// A message to display or log
        /// </summary>
        /// <param name="message">The message</param>
        public ConverterMessageEventArgs(string message)
        {
            if (message == null) { throw new ArgumentNullException("message"); }
            this.Message = message;
        }
    }
}
