using NUnit.Framework;
using Pitech.XR.ContentDelivery;
using Pitech.XR.ContentDelivery.Editor;
using UnityEngine;

namespace Pitech.XR.ContentDelivery.Editor.Tests
{
    public class AddressablesServiceBuildCcdRemoteLoadPathTests
    {
        [Test]
        public void BuildCcdRemoteLoadPath_FullOverride_WinsOverTemplateAndBucket()
        {
            var config = ScriptableObject.CreateInstance<AddressablesModuleConfig>();
            config.ccdRemoteLoadPathTemplate = "https://x.example/buckets/{bucketId}/";
            config.environment = ContentDeliveryEnvironment.Production;

            string result = AddressablesService.BuildCcdRemoteLoadPath(config, "bucket-ignored", "  https://override/full/path  ");

            Assert.AreEqual("https://override/full/path", result);
        }

        [Test]
        public void BuildCcdRemoteLoadPath_EmptyBucket_ReturnsNull()
        {
            var config = ScriptableObject.CreateInstance<AddressablesModuleConfig>();
            config.ccdRemoteLoadPathTemplate = "https://x/buckets/{bucketId}/";

            Assert.IsNull(AddressablesService.BuildCcdRemoteLoadPath(config, null, null));
            Assert.IsNull(AddressablesService.BuildCcdRemoteLoadPath(config, "   ", null));
            Assert.IsNull(AddressablesService.BuildCcdRemoteLoadPath(config, "", null));
        }

        [Test]
        public void BuildCcdRemoteLoadPath_EmptyTemplate_ReturnsNull()
        {
            var config = ScriptableObject.CreateInstance<AddressablesModuleConfig>();
            config.ccdRemoteLoadPathTemplate = "";

            Assert.IsNull(AddressablesService.BuildCcdRemoteLoadPath(config, "abc", null));
        }

        [Test]
        public void BuildCcdRemoteLoadPath_ReplacesBucketIdAndEnvironment()
        {
            var config = ScriptableObject.CreateInstance<AddressablesModuleConfig>();
            config.environment = ContentDeliveryEnvironment.Staging;
            config.ccdRemoteLoadPathTemplate =
                "https://host/client_api/v1/environments/{environment}/buckets/{bucketId}/release_by_badge/latest/entry_by_path/content/?path=";

            string result = AddressablesService.BuildCcdRemoteLoadPath(config, "my-bucket-id", null);

            Assert.AreEqual(
                "https://host/client_api/v1/environments/staging/buckets/my-bucket-id/release_by_badge/latest/entry_by_path/content/?path=",
                result);
        }

        [Test]
        public void BuildCcdRemoteLoadPath_FallsBackToRemoteLoadPathTemplate_WhenCcdFieldEmpty()
        {
            var config = ScriptableObject.CreateInstance<AddressablesModuleConfig>();
            config.ccdRemoteLoadPathTemplate = string.Empty;
            config.environment = ContentDeliveryEnvironment.Development;
            // Typical "always production" CCD URL in Remote Load Path only (CCD field left empty).
            config.remoteLoadPathTemplate =
                "https://proj.client-api.unity3dusercontent.com/client_api/v1/environments/production/buckets/{bucketId}/release_by_badge/latest/entry_by_path/content/?path=";

            string result = AddressablesService.BuildCcdRemoteLoadPath(config, "bucket-uuid", null);

            Assert.AreEqual(
                "https://proj.client-api.unity3dusercontent.com/client_api/v1/environments/production/buckets/bucket-uuid/release_by_badge/latest/entry_by_path/content/?path=",
                result);
        }
    }
}
