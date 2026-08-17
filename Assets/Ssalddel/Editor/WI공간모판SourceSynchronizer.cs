using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;

namespace Ssalddel.Unity.Editor
{
    internal static class WI공간모판SourceSynchronizer
    {
        internal static bool Sync(string mirrorRoot)
        {
            var sourceRoot = WI공간모판AuthoringSource.AuthoritativeRoot();
            var catalogPath = Path.Combine(sourceRoot, "catalog.json");
            if (!File.Exists(catalogPath))
                throw new InvalidOperationException("WiSpatialSeedbedAuthoritativeCatalogMissing:" + catalogPath);

            var sourceCatalog = WI공간모판AuthoringSource.ReadJson<WI공간모판SourceCatalog>(catalogPath);
            Directory.CreateDirectory(mirrorRoot);
            Directory.CreateDirectory(Path.Combine(mirrorRoot, "definitions"));

            var changed = false;
            var receipts = new List<WI공간모판SourceReceiptFile>();
            var catalogHash = WI공간모판AuthoringSource.Sha256(catalogPath);
            changed |= CopyIfChanged(catalogPath, Path.Combine(mirrorRoot, "catalog.json"), catalogHash);
            receipts.Add(new WI공간모판SourceReceiptFile
            {
                RelativePath = "catalog.json",
                Sha256 = catalogHash,
            });

            foreach (var definitionRef in sourceCatalog.DefinitionRefs)
            {
                WI공간모판AuthoringSource.ValidateRelativeJsonPath(definitionRef);
                var source = Path.Combine(sourceRoot,
                    definitionRef.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(source))
                    throw new InvalidOperationException("WiSpatialSeedbedDefinitionMissing:" + definitionRef);
                var destination = Path.Combine(mirrorRoot,
                    definitionRef.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var hash = WI공간모판AuthoringSource.Sha256(source);
                changed |= CopyIfChanged(source, destination, hash);
                receipts.Add(new WI공간모판SourceReceiptFile
                {
                    RelativePath = definitionRef,
                    Sha256 = hash,
                });
            }

            var receipt = new WI공간모판SourceReceipt
            {
                SchemaVersion = "wi-spatial-seedbed-unity-source-receipt.v1",
                SourceProject = "Hongdal",
                SourceRelativeRoot = WI공간모판AuthoringSource.AuthoritativeRelativeRoot,
                CatalogRevision = sourceCatalog.Revision,
                Files = receipts.ToArray(),
                PresentationOnly = true,
            };
            var receiptPath = Path.Combine(mirrorRoot, "source-receipt.json");
            var receiptText = JsonConvert.SerializeObject(receipt, Formatting.Indented)
                + Environment.NewLine;
            changed |= WriteIfChanged(receiptPath, receiptText);

            if (changed)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return changed;
        }

        private static bool CopyIfChanged(string source, string destination, string sourceHash)
        {
            if (File.Exists(destination)
                && new FileInfo(source).Length == new FileInfo(destination).Length
                && string.Equals(sourceHash, WI공간모판AuthoringSource.Sha256(destination),
                    StringComparison.Ordinal))
                return false;
            File.Copy(source, destination, true);
            return true;
        }

        private static bool WriteIfChanged(string path, string content)
        {
            if (File.Exists(path)
                && string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal))
                return false;
            File.WriteAllText(path, content);
            return true;
        }
    }
}
