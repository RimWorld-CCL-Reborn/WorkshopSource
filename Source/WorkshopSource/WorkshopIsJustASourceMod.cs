namespace WorkshopSource
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Harmony;
    using RimWorld;
    using UnityEngine;
    using Verse;
    public class WorkshopIsJustASourceMod : Mod
    {


        public WorkshopIsJustASourceMod(ModContentPack content) : base(content: content)
        {
            HarmonyInstance harmony = HarmonyInstance.Create(id: "rimworld.erdelf.workshopsource");
            harmony.Patch(original: AccessTools.Method(type: typeof(ModLister), name: "RebuildModList"), postfix: new HarmonyMethod(type: typeof(WorkshopIsJustASourceMod), name: nameof(PreOpen)));
            harmony.Patch(original: AccessTools.Method(type: typeof(Page_ModsConfig), name: nameof(Page_ModsConfig.DoWindowContents)), postfix: new HarmonyMethod(type: typeof(WorkshopIsJustASourceMod), name: nameof(DrawButtons)));
        }

        public static List<ModMetaData> workshopMods;
        public static List<ModMetaData> nonWorkshopMods;

        public static IEnumerable<KeyValuePair<ModMetaData, ModMetaData>> outdatedMods;

        public static void PreOpen()
        {
            Debug.Log("");
            workshopMods = ModLister.AllInstalledMods.Where(predicate: mmd => mmd.OnSteamWorkshop).ToList();
            Traverse.Create(type: typeof(ModLister)).Field<List<ModMetaData>>(name: "mods").Value = nonWorkshopMods = ModLister.AllInstalledMods.Where(predicate: mmd => !mmd.OnSteamWorkshop).ToList();
            outdatedMods = OutdatedMods();
        }

        public static void DrawButtons(Page_ModsConfig __instance, Rect rect)
        {
            Rect eRect = rect.ExpandedBy(margin: 18f);
            Widgets.Label(rect: new Rect(x: 0f, y: eRect.height - 80f, width: 200f, height: 40f), label: $"{workshopMods.Count} Workshop mods\n{outdatedMods.Count()} Mods outdated.");
            if (!Widgets.ButtonText(rect: new Rect(x: 140f, y: eRect.height - 80f, width: 120f, height: 40f), label: "Update")) return;
            Find.WindowStack.Add(window: new Dialog_MessageBox(text: string.Join(separator: "\n", value: outdatedMods.Select(selector: kvp => kvp.Key.Name).ToArray()), acceptAction: () =>
            {
                foreach (KeyValuePair<ModMetaData, ModMetaData> kvp in outdatedMods)
                {
                    kvp.Value?.RootDir.Delete(recursive: true);

                    TryCreateLocalCopy(mod: kvp.Key, copy: out ModMetaData _);
                }
                ModLister.RebuildModList();
            }));
        }

        public static bool TryCreateLocalCopy(ModMetaData mod, out ModMetaData copy)
        {
            copy = null;

            if (mod.Source != ContentSource.SteamWorkshop)
            {
                Log.Error(text: "Can only create local copies of steam workshop mods.");
                return false;
            }

            string targetDir = Path.Combine(path1: GenFilePaths.CoreModsFolderPath, path2: mod.Name);

            try
            {
                Copy(source: mod.RootDir, destination: targetDir, recursive: true);
                copy = new ModMetaData(localAbsPath: targetDir);
                (ModLister.AllInstalledMods as List<ModMetaData>)?.Add(item: copy);
                return true;
            }
            catch (Exception e)
            {
                Log.Error(text: "Creating local copy failed: " + e.Message);
                return false;
            }
        }

        private static void Copy(DirectoryInfo source, string destination, bool recursive)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo[] dirs = source.GetDirectories();

            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(path: destination))
            {
                Directory.CreateDirectory(path: destination);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = source.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(path1: destination, path2: file.Name);
                file.CopyTo(destFileName: temppath, overwrite: false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (recursive)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(path1: destination, path2: subdir.Name);
                    Copy(source: subdir, destination: temppath, recursive: true);
                }
            }
        }





        public static IEnumerable<KeyValuePair<ModMetaData, ModMetaData>> OutdatedMods()
        {
            foreach (ModMetaData mod in workshopMods)
            {
                ModMetaData modTwo = nonWorkshopMods.FirstOrDefault(predicate: modd => IsMod(modOne: mod, modTwo: modd));
                if (modTwo == null || IsOutdated(modOne: mod, modTwo: modTwo))
                    yield return new KeyValuePair<ModMetaData, ModMetaData>(key: mod, value: modTwo);
            }
        }

        public static bool IsMod(ModMetaData modOne, ModMetaData modTwo)
        {
            return modOne.Name == modTwo.Name;
        }

        private static readonly FileCompare compare = new FileCompare();

        public static bool IsOutdated(ModMetaData modOne, ModMetaData modTwo)
        {
            return !modOne.RootDir.GetFiles(searchPattern: "*.*", searchOption: SearchOption.AllDirectories).SequenceEqual(second: modTwo.RootDir.GetFiles(searchPattern: "*.*", searchOption: SearchOption.AllDirectories), comparer: compare);
        }

        // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/how-to-compare-the-contents-of-two-folders-linq
        // This implementation defines a very simple comparison  
        // between two FileInfo objects. It only compares the name  
        // of the files being compared and their length in bytes.  
        private class FileCompare : IEqualityComparer<FileInfo>
        {
            public bool Equals(FileInfo f1, FileInfo f2) => f1.Name   == f2.Name &&
                                                            f1.Length == f2.Length;

            // Return a hash that reflects the comparison criteria. According to the   
            // rules for IEqualityComparer<T>, if Equals is true, then the hash codes must  
            // also be equal. Because equality as defined here is a simple value equality, not  
            // reference identity, it is possible that two or more objects will produce the same  
            // hash code.  
            public int GetHashCode(FileInfo fi)
            {
                string s = $"{fi.Name}{fi.Length}";
                return s.GetHashCode();
            }
        }
    }
}
