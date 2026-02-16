using NUnit.Framework;
using Pitech.XR.ContentDelivery;

namespace Pitech.XR.ContentDelivery.Editor.Tests
{
    public class PublishTransactionIdempotencyTests
    {
        [Test]
        public void BuildKey_IsDeterministic()
        {
            string keyA = PublishTransactionIdempotency.BuildKey(
                "tenant-1",
                "lab-1",
                "ver-1",
                "hash-1");
            string keyB = PublishTransactionIdempotency.BuildKey(
                "tenant-1",
                "lab-1",
                "ver-1",
                "hash-1");

            Assert.AreEqual(keyA, keyB);
            Assert.AreEqual("publish:tenant-1:lab-1:ver-1:hash-1", keyA);
        }

        [Test]
        public void ContentFingerprint_ReturnsStableHex()
        {
            string hashA = PublishTransactionIdempotency.ComputeContentFingerprint("same-input");
            string hashB = PublishTransactionIdempotency.ComputeContentFingerprint("same-input");

            Assert.AreEqual(hashA, hashB);
            Assert.AreEqual(64, hashA.Length);
        }
    }
}
