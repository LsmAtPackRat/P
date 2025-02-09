﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace PChecker.Actors.Events
{
    /// <summary>
    /// A default event that is generated by the runtime when
    /// no user-defined event is dequeued or received.
    /// </summary>
    [DataContract]
    public sealed class DefaultEvent : Event
    {
        /// <summary>
        /// Gets a <see cref="DefaultEvent"/> instance.
        /// </summary>
        public static DefaultEvent Instance { get; } = new DefaultEvent();

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultEvent"/> class.
        /// </summary>
        private DefaultEvent()
            : base()
        {
        }
    }
}
