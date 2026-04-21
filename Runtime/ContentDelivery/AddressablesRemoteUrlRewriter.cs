using System;
using UnityEngine;

#if PITECH_ADDR
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
#endif

namespace Pitech.XR.ContentDelivery
{
    /// <summary>
    /// Rewrites Addressables bundle URLs at runtime so that bundles referenced by a catalog
    /// are always fetched from the same CCD release whose catalog URL arrived in the
    /// LaunchContext. This decouples builds from `release_by_badge/latest` (mutable,
    /// breaks cohort pinning) and from any specific `releases/&lt;id&gt;` baked at build
    /// time.
    ///
    /// Only URLs matching the Unity CCD client_api pattern AND the bucket of the current
    /// runtime URL are rewritten; anything else (local files, other domains, other buckets)
    /// passes through unchanged.
    /// </summary>
    public static class AddressablesRemoteUrlRewriter
    {
        const string BucketsMarker = "/buckets/";
        const string EntryByPathMarker = "/entry_by_path/content/";

        static readonly object Sync = new object();
        static string _runtimeBucketId;
        static string _runtimeReleaseBase;
        static bool _installed;

        public static bool IsInstalled
        {
            get { lock (Sync) { return _installed; } }
        }

        public static string CurrentReleaseBase
        {
            get { lock (Sync) { return _runtimeReleaseBase; } }
        }

        public static void ApplyFrom(LaunchContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.runtimeUrl))
            {
                return;
            }

            if (!TryParseCcdUrl(context.runtimeUrl, out string bucketId, out string releaseBase, out _))
            {
                // Runtime URL isn't a recognized CCD URL — nothing to rewrite.
                return;
            }

            lock (Sync)
            {
                bool changed =
                    !string.Equals(_runtimeBucketId, bucketId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(_runtimeReleaseBase, releaseBase, StringComparison.Ordinal);

                _runtimeBucketId = bucketId;
                _runtimeReleaseBase = releaseBase;

                if (!_installed)
                {
                    Install();
                }

                if (changed)
                {
                    Debug.Log($"[AddressablesRemoteUrlRewriter] Active — bucket={bucketId}, releaseBase={releaseBase}");
                }
            }
        }

        public static void Clear()
        {
            lock (Sync)
            {
                Uninstall();
                _runtimeBucketId = null;
                _runtimeReleaseBase = null;
            }
        }

        /// <summary>
        /// Pure URL rewrite — exposed for unit testing. Returns <paramref name="url"/>
        /// unchanged if it is not a CCD client_api URL or if its bucket differs from
        /// <paramref name="expectedBucketId"/>.
        /// </summary>
        public static string RewriteUrl(string url, string expectedBucketId, string targetReleaseBase)
        {
            if (string.IsNullOrEmpty(url) ||
                string.IsNullOrEmpty(expectedBucketId) ||
                string.IsNullOrEmpty(targetReleaseBase))
            {
                return url;
            }

            if (!TryParseCcdUrl(url, out string urlBucketId, out string urlReleaseBase, out string tail))
            {
                return url;
            }

            if (!string.Equals(urlBucketId, expectedBucketId, StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            if (string.Equals(urlReleaseBase, targetReleaseBase, StringComparison.Ordinal))
            {
                return url;
            }

            return targetReleaseBase + tail;
        }

        static void Install()
        {
#if PITECH_ADDR
            Addressables.ResourceManager.InternalIdTransformFunc = TransformLocation;
            _installed = true;
#else
            // No Addressables — nothing to install, but remember intent for logging.
            _installed = false;
#endif
        }

        static void Uninstall()
        {
#if PITECH_ADDR
            if (_installed)
            {
                Addressables.ResourceManager.InternalIdTransformFunc = null;
            }
#endif
            _installed = false;
        }

#if PITECH_ADDR
        static string TransformLocation(IResourceLocation location)
        {
            string id = location?.InternalId;
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }

            string bucket;
            string releaseBase;
            lock (Sync)
            {
                bucket = _runtimeBucketId;
                releaseBase = _runtimeReleaseBase;
            }

            return RewriteUrl(id, bucket, releaseBase);
        }
#endif

        /// <summary>
        /// Parses a Unity CCD client_api URL into its bucket id and release-base prefix.
        /// The release base includes the trailing "/entry_by_path/content/" so callers
        /// can concatenate the tail (query string / path) directly.
        /// Accepts both `releases/&lt;id&gt;` and `release_by_badge/&lt;badge&gt;` variants.
        /// </summary>
        public static bool TryParseCcdUrl(string url, out string bucketId, out string releaseBase, out string tail)
        {
            bucketId = null;
            releaseBase = null;
            tail = null;

            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            int bucketsIdx = url.IndexOf(BucketsMarker, StringComparison.OrdinalIgnoreCase);
            if (bucketsIdx < 0)
            {
                return false;
            }

            int bucketStart = bucketsIdx + BucketsMarker.Length;
            int bucketEnd = url.IndexOf('/', bucketStart);
            if (bucketEnd <= bucketStart)
            {
                return false;
            }

            int entryIdx = url.IndexOf(EntryByPathMarker, bucketEnd, StringComparison.OrdinalIgnoreCase);
            if (entryIdx < 0)
            {
                return false;
            }

            int baseEnd = entryIdx + EntryByPathMarker.Length;
            bucketId = url.Substring(bucketStart, bucketEnd - bucketStart);
            releaseBase = url.Substring(0, baseEnd);
            tail = url.Substring(baseEnd);
            return true;
        }
    }
}
