using NUnit.Framework;
using Pitech.XR.ContentDelivery;
using Pitech.XR.ContentDelivery.Editor;
using UnityEngine;

namespace Pitech.XR.ContentDelivery.Editor.Tests
{
    public class AddressablesVersionedLocalPathsTests
    {
        [Test]
        public void NormalizeVersionSegment_PreservesSemverDots()
        {
            Assert.AreEqual("1.13", AddressablesService.NormalizeVersionSegment("1.13"));
            Assert.AreEqual("2.0.1", AddressablesService.NormalizeVersionSegment("  2.0.1  "));
        }

        [Test]
        public void NormalizeVersionSegment_ReplacesInvalidCharacters()
        {
            Assert.AreEqual("1-13-rc", AddressablesService.NormalizeVersionSegment("1:13 rc"));
        }

        [Test]
        public void BuildLocalLabVersionRoot_BuildsExpectedPath()
        {
            var config = ScriptableObject.CreateInstance<AddressablesModuleConfig>();
            config.localWorkspaceRoot = "Build/ContentDelivery";

            string root = AddressablesService.BuildLocalLabVersionRoot(config, "Default", "endomiikes", "1.13");

            Assert.AreEqual("Build/ContentDelivery/Addressables/Default/endomiikes/1.13", root);
        }

        [Test]
        public void BuildLocalLabVersionRoot_CustomProfileAndWorkspace()
        {
            var config = ScriptableObject.CreateInstance<AddressablesModuleConfig>();
            config.localWorkspaceRoot = "Out/CD";

            string root = AddressablesService.BuildLocalLabVersionRoot(config, "Prod", "my-lab", "v2");

            Assert.AreEqual("Out/CD/Addressables/Prod/my-lab/v2", root);
        }

        [Test]
        public void BuildLocalLabVersionRoot_ReturnsEmpty_WhenLabOrVersionMissing()
        {
            var config = ScriptableObject.CreateInstance<AddressablesModuleConfig>();

            Assert.AreEqual(string.Empty, AddressablesService.BuildLocalLabVersionRoot(config, "Default", "", "1.0"));
            Assert.AreEqual(string.Empty, AddressablesService.BuildLocalLabVersionRoot(config, "Default", "lab", ""));
            Assert.AreEqual(string.Empty, AddressablesService.BuildLocalLabVersionRoot(config, "Default", "lab", "   "));
        }
    }
}
