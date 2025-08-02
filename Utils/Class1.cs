using Android.Content;
using Android.Content.PM;
using Android.Provider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hajk
{
    internal class Class1
    {
        public class StorageProvider
        {
            public string Name { get; set; }
            public string PackageName { get; set; }
            public string Authority { get; set; } // optional, for SAF detection
        }

        public static List<StorageProvider> GetInstalledStorageProviders(Context context)
        {
            var knownProviders = new List<StorageProvider>
            {
                new StorageProvider { Name = "Google Drive", PackageName = "com.google.android.apps.docs", Authority = "com.google.android.apps.docs.storage" },
                new StorageProvider { Name = "Dropbox", PackageName = "com.dropbox.android" },
                new StorageProvider { Name = "OneDrive", PackageName = "com.microsoft.skydrive" },
                new StorageProvider { Name = "Samsung My Files", PackageName = "com.sec.android.app.myfiles" },
                new StorageProvider { Name = "ASUS File Manager", PackageName = "com.asus.filemanager" },
                new StorageProvider { Name = "LG File Manager", PackageName = "com.lge.filemanager" },
                new StorageProvider { Name = "Huawei File Manager", PackageName = "com.huawei.hidisk" },
                new StorageProvider { Name = "XOS File Manager", PackageName = "com.transsion.XOSFileManager" },
                new StorageProvider { Name = "Mi File Manager", PackageName = "com.mi.android.globalFileexplorer" },
                new StorageProvider { Name = "Files by Google", PackageName = "com.google.android.apps.nbu.files" },

                // Core system SAF components (will usually be present)
                new StorageProvider { Name = "External Storage", PackageName = "com.android.externalstorage", Authority = "com.android.externalstorage.documents" },
                new StorageProvider { Name = "Downloads", PackageName = "com.android.providers.downloads", Authority = "com.android.providers.downloads.documents" },
                new StorageProvider { Name = "Media", PackageName = "com.android.providers.media", Authority = "com.android.providers.media.documents" },
                new StorageProvider { Name = "DocumentsUI", PackageName = "com.android.documentsui" }
            };
            return null;

            var pm = context.PackageManager;
            var installed = new List<StorageProvider>();

            foreach (var provider in knownProviders)
            {
                try
                {
                    var info = pm.GetPackageInfo(provider.PackageName, 0);
                    if (info != null)
                    {
                        installed.Add(provider);
                    }
                }
                catch (PackageManager.NameNotFoundException)
                {
                    // Not installed
                }
            }

            return installed;
        }


        public static List<string> GetSafProviders(Context context)
        {
            GetInstalledStorageProviders(context);

            return null;
        }

    }
}
