﻿namespace RegistrationState
{
    using System;
    using System.Threading.Tasks;
    using Automatonymous;
    using GreenPipes;
    using MassTransit;
    using MassTransit.Logging;
    using MassTransit.Util;
    using Registration.Contracts;


    public class RegistrationStateMachine :
        MassTransitStateMachine<RegistrationStateInstance>
    {
        static readonly ILog _log = Logger.Get<RegistrationStateMachine>();

        public RegistrationStateMachine()
        {
            InstanceState(x => x.CurrentState);

            Event(() => EventRegistrationReceived, x =>
            {
                x.CorrelateById(m => m.Message.SubmissionId);
                x.SelectId(m => m.Message.SubmissionId);
            });

            Event(() => EventRegistrationCompleted, x =>
            {
                x.CorrelateById(m => m.Message.SubmissionId);
            });

            Event(() => LicenseVerificationFailed, x =>
            {
                x.CorrelateById(m => m.Message.SubmissionId);
            });

            Event(() => PaymentFailed, x =>
            {
                x.CorrelateById(m => m.Message.SubmissionId);
            });

            Initially(
                When(EventRegistrationReceived)
                    .Then(Initialize)
                    .ThenAsync(InitiateProcessing)
                    .TransitionTo(Received));

            During(Received,
                When(EventRegistrationCompleted)
                    .Then(Register)
                    .TransitionTo(Registered),
                When(LicenseVerificationFailed)
                    .Then(InvalidLicense)
                    .TransitionTo(Suspended),
                When(PaymentFailed)
                    .Then(PaymentFailure)
                    .TransitionTo(Suspended));

            During(Suspended,
                When(EventRegistrationReceived)
                    .Then(Initialize)
                    .ThenAsync(InitiateProcessing)
                    .TransitionTo(Received));
        }

        public State Received { get; private set; }
        public State Registered { get; private set; }
        public State Suspended { get; private set; }

        public Event<RegistrationReceived> EventRegistrationReceived { get; private set; }
        public Event<RegistrationCompleted> EventRegistrationCompleted { get; private set; }
        public Event<RegistrationLicenseVerificationFailed> LicenseVerificationFailed { get; private set; }
        public Event<RegistrationPaymentFailed> PaymentFailed { get; private set; }

        void Initialize(BehaviorContext<RegistrationStateInstance, RegistrationReceived> context)
        {
            InitializeInstance(context.Instance, context.Data);
        }

        void Register(BehaviorContext<RegistrationStateInstance, RegistrationCompleted> context)
        {
            _log.InfoFormat("Registered: {0} ({1})", context.Data.SubmissionId, context.Instance.ParticipantEmailAddress);
        }

        void InvalidLicense(BehaviorContext<RegistrationStateInstance, RegistrationLicenseVerificationFailed> context)
        {
            _log.InfoFormat("Invalid License: {0} ({1}) - {2}", context.Data.SubmissionId, context.Instance.ParticipantLicenseNumber, context.Data.ExceptionInfo.Message);
        }

        void PaymentFailure(BehaviorContext<RegistrationStateInstance, RegistrationPaymentFailed> context)
        {
            _log.InfoFormat("Payment Failed: {0} ({1}) - {2}", context.Data.SubmissionId, context.Instance.ParticipantEmailAddress, context.Data.ExceptionInfo.Message);
        }

        async Task InitiateProcessing(BehaviorContext<RegistrationStateInstance, RegistrationReceived> context)
        {
            var registration = CreateProcessRegistration(context.Data);

            Uri destinationAddress;
            if (!EndpointConvention.TryGetDestinationAddress(registration, out destinationAddress))
            {
                throw new ConfigurationException($"The endpoint convention was not configured: {TypeMetadataCache<ProcessRegistration>.ShortName}");
            }

            await context.GetPayload<ConsumeContext>().Send(destinationAddress, registration).ConfigureAwait(false);

            _log.InfoFormat("Processing: {0} ({1})", context.Data.SubmissionId, context.Data.ParticipantEmailAddress);
        }

        static void InitializeInstance(RegistrationStateInstance instance, RegistrationReceived data)
        {
            _log.InfoFormat("Initializing: {0} ({1})", data.SubmissionId, data.ParticipantEmailAddress);

            instance.ParticipantEmailAddress = data.ParticipantEmailAddress;
            instance.ParticipantLicenseNumber = data.ParticipantLicenseNumber;
            instance.ParticipantCategory = data.ParticipantCategory;

            instance.EventId = data.EventId;
            instance.RaceId = data.RaceId;
        }

        static ProcessRegistration CreateProcessRegistration(RegistrationReceived message)
        {
            return new Process(message.SubmissionId, message.ParticipantEmailAddress, message.ParticipantLicenseNumber, message.ParticipantCategory,
                message.EventId, message.RaceId, message.CardNumber);
        }


        class Process :
            ProcessRegistration
        {
            public Process(Guid submissionId, string participantEmailAddress, string participantLicenseNumber, string participantCategory, string eventId,
                string raceId, string cardNumber)
            {
                SubmissionId = submissionId;
                ParticipantEmailAddress = participantEmailAddress;
                ParticipantLicenseNumber = participantLicenseNumber;
                ParticipantCategory = participantCategory;
                EventId = eventId;
                RaceId = raceId;
                CardNumber = cardNumber;

                Timestamp = DateTime.UtcNow;
            }

            public Guid SubmissionId { get; }
            public DateTime Timestamp { get; }
            public string ParticipantEmailAddress { get; }
            public string ParticipantLicenseNumber { get; }
            public string ParticipantCategory { get; }
            public string EventId { get; }
            public string RaceId { get; }
            public string CardNumber { get; }
        }
    }
}