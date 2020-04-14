﻿using System;

namespace FireOnWheels.Messaging
{
    public interface IOrderRegisteredEvent
    {
        Guid CorrelationId { get; }

        DateTime RegisteredDateTime { get; set; }
    }
}
