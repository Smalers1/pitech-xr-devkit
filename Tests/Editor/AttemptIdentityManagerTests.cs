using NUnit.Framework;
using Pitech.XR.ContentDelivery;

namespace Pitech.XR.ContentDelivery.Editor.Tests
{
    public class AttemptIdentityManagerTests
    {
        [Test]
        public void CreateLocalFirst_GeneratesAllIdentifiers()
        {
            AttemptIdentity identity = AttemptIdentityManager.CreateLocalFirst("lab-abc");

            Assert.IsNotEmpty(identity.launchRequestId);
            Assert.IsNotEmpty(identity.attemptId);
            Assert.IsNotEmpty(identity.idempotencyKey);
            Assert.IsTrue(identity.isLocalOnly);
            Assert.IsFalse(identity.isReconciled);
        }

        [Test]
        public void Reconcile_UpdatesCanonicalAttempt()
        {
            AttemptIdentity identity = AttemptIdentityManager.CreateLocalFirst("lab-abc");

            bool success = AttemptIdentityManager.TryReconcile(identity.launchRequestId, "backend-attempt-42");
            Assert.IsTrue(success);

            bool found = AttemptIdentityManager.TryGet(identity.launchRequestId, out AttemptIdentity stored);
            Assert.IsTrue(found);
            Assert.IsTrue(stored.isReconciled);
            Assert.AreEqual("backend-attempt-42", stored.canonicalAttemptId);
            Assert.IsFalse(stored.isLocalOnly);
        }
    }
}
