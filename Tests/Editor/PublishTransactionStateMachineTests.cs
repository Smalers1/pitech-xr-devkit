using NUnit.Framework;
using Pitech.XR.ContentDelivery;

namespace Pitech.XR.ContentDelivery.Editor.Tests
{
    public class PublishTransactionStateMachineTests
    {
        [Test]
        public void CreateDraft_InitializesDraftStateAndHistory()
        {
            PublishTransactionReportData report =
                PublishTransactionFactory.CreateDraft(PublishTransactionSource.GuidedSetup, "tests");

            Assert.AreEqual(PublishTransactionState.Draft, report.state);
            Assert.AreEqual(1, report.stateHistory.Count);
            Assert.AreEqual(PublishTransactionState.Draft, report.stateHistory[0].toState);
        }

        [Test]
        public void StateMachine_AllowsValidBuildPath()
        {
            PublishTransactionReportData report =
                PublishTransactionFactory.CreateDraft(PublishTransactionSource.HiddenBuild, "tests");

            Assert.IsTrue(PublishTransactionStateMachine.TryTransition(
                report,
                PublishTransactionState.Validating,
                "begin",
                "tests"));
            Assert.IsTrue(PublishTransactionStateMachine.TryTransition(
                report,
                PublishTransactionState.Validated,
                "ok",
                "tests"));
            Assert.IsTrue(PublishTransactionStateMachine.TryTransition(
                report,
                PublishTransactionState.BuildRequested,
                "request",
                "tests"));
            Assert.IsTrue(PublishTransactionStateMachine.TryTransition(
                report,
                PublishTransactionState.Building,
                "run",
                "tests"));
            Assert.IsTrue(PublishTransactionStateMachine.TryTransition(
                report,
                PublishTransactionState.Built,
                "done",
                "tests"));
            Assert.AreEqual(PublishTransactionState.Built, report.state);
        }

        [Test]
        public void StateMachine_RejectsInvalidTransition()
        {
            PublishTransactionReportData report =
                PublishTransactionFactory.CreateDraft(PublishTransactionSource.GuidedSetup, "tests");

            bool allowed = PublishTransactionStateMachine.TryTransition(
                report,
                PublishTransactionState.Built,
                "skip",
                "tests");

            Assert.IsFalse(allowed);
            Assert.AreEqual(PublishTransactionState.Draft, report.state);
        }
    }
}
