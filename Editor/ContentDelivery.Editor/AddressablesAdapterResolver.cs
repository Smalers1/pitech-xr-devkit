#if UNITY_EDITOR
using System;

namespace Pitech.XR.ContentDelivery.Editor
{
    public static class AddressablesAdapterResolver
    {
        public static IAddressablesConventionAdapter Resolve(AddressablesModuleConfig config)
        {
            if (config != null && !string.IsNullOrWhiteSpace(config.adapterTypeName))
            {
                Type adapterType = Type.GetType(config.adapterTypeName, throwOnError: false);
                if (adapterType != null && typeof(IAddressablesConventionAdapter).IsAssignableFrom(adapterType))
                {
                    object adapter = Activator.CreateInstance(adapterType);
                    if (adapter is IAddressablesConventionAdapter typed)
                    {
                        return typed;
                    }
                }
            }

            Type healthOnType = Type.GetType(
                "Pitech.XR.ContentDelivery.Editor.HealthOnAddressablesAdapter, Pitech.XR.ContentDelivery.Editor",
                throwOnError: false);
            if (healthOnType != null && typeof(IAddressablesConventionAdapter).IsAssignableFrom(healthOnType))
            {
                object healthOn = Activator.CreateInstance(healthOnType);
                if (healthOn is IAddressablesConventionAdapter typed)
                {
                    return typed;
                }
            }

            return new DefaultAddressablesConventionAdapter();
        }
    }
}
#endif
