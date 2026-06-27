// ============================================================
//  MvBlockReader.cs
//  AutoCAD Architecture 2022 – MvBlock Eigenschaften auslesen
//
//  Benötigte Referenzen (.csproj):
//    accoremgd.dll   → AutoCAD Core Managed
//    acdbmgd.dll     → AutoCAD Database Managed
//    acmgd.dll       → AutoCAD Managed
//    AecBaseMgd.dll  → ACA Base (MvBlockReference, MvBlockDef)
//    AecArchMgd.dll  → ACA Architecture
//
//  Pfad (Standard):
//    C:\Program Files\Autodesk\AutoCAD Architecture 2022\
//
//  Plugin laden in ACA: NETLOAD → MvBlockReader.dll
//  Befehle:
//    MVBLOCK_READ  – per Klick einen MvBlock auswählen
//    MVBLOCK_ALL   – alle MvBlocks im Modellbereich auslesen
// ============================================================

using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Aec.DatabaseServices;   // MvBlockReference, MvBlockDef, MvViewBlock

[assembly: CommandClass(typeof(MvBlockReader.MvBlockCommands))]

namespace MvBlockReader
{
    // ──────────────────────────────────────────────────────────
    // Datenmodelle
    // ──────────────────────────────────────────────────────────

    /// <summary>Vollständige Eigenschaften einer MvBlock-Instanz.</summary>
    public class MvBlockData
    {
        // Basis
        public string Name         { get; set; }
        public string Handle       { get; set; }
        public string Layer        { get; set; }
        public string ObjectClass  { get; set; }

        // Geometrie
        public double PosX         { get; set; }
        public double PosY         { get; set; }
        public double PosZ         { get; set; }
        public double RotationDeg  { get; set; }
        public double ScaleX       { get; set; }
        public double ScaleY       { get; set; }
        public double ScaleZ       { get; set; }

        // ACA-spezifisch
        public string MvBlockDefName    { get; set; }
        public bool   IsAnnotative      { get; set; }
        public int    ViewCount         { get; set; }

        // Untergeordnete Listen
        public List<AttributeData>  Attributes { get; set; } = new List<AttributeData>();
        public List<XDataEntry>     XDataList  { get; set; } = new List<XDataEntry>();
        public List<ViewDefinition> Views      { get; set; } = new List<ViewDefinition>();
    }

    /// <summary>Ein Attribut der BlockReference.</summary>
    public class AttributeData
    {
        public string Tag       { get; set; }
        public string Value     { get; set; }
        public string Prompt    { get; set; }
        public string Layer     { get; set; }
        public double Height    { get; set; }
        public bool   Invisible { get; set; }
        public bool   Constant  { get; set; }
    }

    /// <summary>Ein Eintrag aus den Extended Entity Data (XData).</summary>
    public class XDataEntry
    {
        public string AppName  { get; set; }
        public int    TypeCode { get; set; }
        public string Value    { get; set; }
    }

    /// <summary>Eine View-Definition des MvBlockDef.</summary>
    public class ViewDefinition
    {
        public string ViewName      { get; set; }
        public string BlockName     { get; set; }
        public string ViewDirection { get; set; }
    }

    // ──────────────────────────────────────────────────────────
    // Befehle
    // ──────────────────────────────────────────────────────────

    public class MvBlockCommands
    {
        // ── BEFEHL 1: Einzelnen MvBlock per Klick auswählen ───
        [CommandMethod("MVBLOCK_READ", CommandFlags.Modal)]
        public void ReadSelectedMvBlock()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db  = doc.Database;
            Editor   ed  = doc.Editor;

            PromptEntityOptions peo = new PromptEntityOptions(
                "\nMvBlock auswählen: ");
            peo.SetRejectMessage("\nBitte eine Block-Referenz auswählen.");
            peo.AddAllowedClass(typeof(BlockReference), false);

            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    DBObject obj = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    MvBlockData data = ExtractData(obj, tr);

                    if (data != null)
                        ed.WriteMessage(BuildReport(data));
                    else
                        ed.WriteMessage("\nGewähltes Objekt konnte nicht ausgelesen werden.");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\nFehler: {ex.Message}");
                }
                tr.Commit();
            }
        }

        // ── BEFEHL 2: Alle MvBlocks im Modellbereich auslesen ─
        [CommandMethod("MVBLOCK_ALL", CommandFlags.Modal)]
        public void ReadAllMvBlocks()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db  = doc.Database;
            Editor   ed  = doc.Editor;

            var results = new List<MvBlockData>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(
                    db.BlockTableId, OpenMode.ForRead);

                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(
                    bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in ms)
                {
                    DBObject obj = tr.GetObject(id, OpenMode.ForRead);
                    MvBlockData data = ExtractData(obj, tr);
                    if (data != null) results.Add(data);
                }

                tr.Commit();
            }

            if (results.Count == 0)
            {
                ed.WriteMessage("\nKeine MvBlocks im Modellbereich gefunden.");
                return;
            }

            ed.WriteMessage($"\n{results.Count} MvBlock(s) gefunden:\n");
            foreach (var d in results)
                ed.WriteMessage(BuildReport(d));
        }

        // ──────────────────────────────────────────────────────
        // Kern-Extraktion
        // ──────────────────────────────────────────────────────

        private MvBlockData ExtractData(DBObject obj, Transaction tr)
        {
            var data = new MvBlockData();

            // ── Versuch als MvBlockReference zu casten ─────────
            MvBlockReference mvRef = obj as MvBlockReference;
            if (mvRef != null)
            {
                data.ObjectClass = "MvBlockReference";
                data.Name        = mvRef.Name;
                data.Handle      = mvRef.Handle.ToString();
                data.Layer       = mvRef.Layer;

                // Geometrie
                Point3d ip      = mvRef.Position;
                data.PosX       = ip.X;
                data.PosY       = ip.Y;
                data.PosZ       = ip.Z;
                data.RotationDeg = mvRef.Rotation * (180.0 / Math.PI);
                data.ScaleX     = mvRef.ScaleFactors.X;
                data.ScaleY     = mvRef.ScaleFactors.Y;
                data.ScaleZ     = mvRef.ScaleFactors.Z;

                // MvBlockDef auslesen
                ReadMvBlockDef(mvRef, tr, data);

                // Attribute & XData
                data.Attributes = ReadAttributes(mvRef, tr);
                data.XDataList  = ReadXData(mvRef);
                return data;
            }

            // ── Fallback: normale BlockReference ───────────────
            BlockReference br = obj as BlockReference;
            if (br != null)
            {
                data.ObjectClass = "BlockReference";
                data.Name        = br.Name;
                data.Handle      = br.Handle.ToString();
                data.Layer       = br.Layer;

                Point3d ip      = br.Position;
                data.PosX       = ip.X;
                data.PosY       = ip.Y;
                data.PosZ       = ip.Z;
                data.RotationDeg = br.Rotation * (180.0 / Math.PI);
                data.ScaleX     = br.ScaleFactors.X;
                data.ScaleY     = br.ScaleFactors.Y;
                data.ScaleZ     = br.ScaleFactors.Z;

                data.Attributes = ReadAttributes(br, tr);
                data.XDataList  = ReadXData(br);
                return data;
            }

            return null; // kein verwertbarer Typ
        }

        // ── MvBlockDef: Name, Annotativ, Views ────────────────
        private void ReadMvBlockDef(
            MvBlockReference mvRef, Transaction tr, MvBlockData data)
        {
            try
            {
                if (mvRef.MvBlockDefId.IsNull) return;

                MvBlockDef def = tr.GetObject(
                    mvRef.MvBlockDefId, OpenMode.ForRead) as MvBlockDef;

                if (def == null) return;

                data.MvBlockDefName = def.Name;
                data.IsAnnotative   = def.Annotative == AnnotativeStates.True;

                // Alle View-Blöcke der Definition durchlaufen
                for (int i = 0; i < def.ViewBlockCount; i++)
                {
                    try
                    {
                        MvViewBlock vb = def.GetViewBlock(i);
                        data.Views.Add(new ViewDefinition
                        {
                            ViewName      = vb.ViewName      ?? "",
                            BlockName     = vb.BlockName     ?? "",
                            ViewDirection = vb.ViewDirection.ToString()
                        });
                    }
                    catch { /* einzelne View überspringen */ }
                }

                data.ViewCount = data.Views.Count;
            }
            catch { /* Definition nicht zugänglich – weiter */ }
        }

        // ── Attribute einer BlockReference auslesen ────────────
        private List<AttributeData> ReadAttributes(
            BlockReference br, Transaction tr)
        {
            var list = new List<AttributeData>();

            foreach (ObjectId attId in br.AttributeCollection)
            {
                try
                {
                    AttributeReference ar = tr.GetObject(
                        attId, OpenMode.ForRead) as AttributeReference;

                    if (ar == null) continue;

                    list.Add(new AttributeData
                    {
                        Tag       = ar.Tag,
                        Value     = ar.TextString,
                        Prompt    = ar.Prompt,
                        Layer     = ar.Layer,
                        Height    = ar.Height,
                        Invisible = ar.Invisible,
                        Constant  = ar.Constant
                    });
                }
                catch { /* Attribut überspringen */ }
            }
            return list;
        }

        // ── XData (Extended Entity Data) auslesen ─────────────
        private List<XDataEntry> ReadXData(DBObject obj)
        {
            var list = new List<XDataEntry>();

            ResultBuffer xdata = obj.XData;
            if (xdata == null) return list;

            string currentApp = "";
            foreach (TypedValue tv in xdata)
            {
                if (tv.TypeCode == (int)DxfCode.ExtendedDataApplicationName)
                    currentApp = tv.Value?.ToString() ?? "";

                list.Add(new XDataEntry
                {
                    AppName  = currentApp,
                    TypeCode = tv.TypeCode,
                    Value    = tv.Value?.ToString() ?? "(null)"
                });
            }
            xdata.Dispose();
            return list;
        }

        // ──────────────────────────────────────────────────────
        // Formatierte Ausgabe
        // ──────────────────────────────────────────────────────

        private string BuildReport(MvBlockData d)
        {
            var sb = new StringBuilder();

            sb.AppendLine("\n╔══════════════════════════════════════════════╗");
            sb.AppendLine($"║  {d.ObjectClass}: {d.Name}");
            sb.AppendLine("╚══════════════════════════════════════════════╝");

            // Basis
            sb.AppendLine($"  Handle       : {d.Handle}");
            sb.AppendLine($"  Layer        : {d.Layer}");

            // ACA-Def
            if (!string.IsNullOrEmpty(d.MvBlockDefName))
            {
                sb.AppendLine($"  Definition   : {d.MvBlockDefName}");
                sb.AppendLine($"  Annotativ    : {d.IsAnnotative}");
                sb.AppendLine($"  View-Anzahl  : {d.ViewCount}");
            }

            // Geometrie
            sb.AppendLine($"  Position     : X={d.PosX,10:F3}  Y={d.PosY,10:F3}  Z={d.PosZ,10:F3}");
            sb.AppendLine($"  Rotation     : {d.RotationDeg:F3}°");
            sb.AppendLine($"  Skalierung   : X={d.ScaleX}  Y={d.ScaleY}  Z={d.ScaleZ}");

            // View-Definitionen
            if (d.Views.Count > 0)
            {
                sb.AppendLine("\n  ┌─ View-Definitionen ──────────────────────");
                foreach (var v in d.Views)
                    sb.AppendLine($"  │  [{v.ViewName,-15}]  Block: {v.BlockName,-20}  " +
                                  $"Richtung: {v.ViewDirection}");
                sb.AppendLine("  └──────────────────────────────────────────");
            }

            // Attribute
            if (d.Attributes.Count > 0)
            {
                sb.AppendLine("\n  ┌─ Attribute ───────────────────────────────");
                foreach (var a in d.Attributes)
                    sb.AppendLine($"  │  {a.Tag,-22} = {a.Value,-20}" +
                                  $"  H={a.Height:F2}  unsichtbar={a.Invisible}");
                sb.AppendLine("  └──────────────────────────────────────────");
            }
            else
            {
                sb.AppendLine("  (keine Attribute)");
            }

            // XData
            if (d.XDataList.Count > 0)
            {
                sb.AppendLine("\n  ┌─ XData ───────────────────────────────────");
                foreach (var x in d.XDataList)
                    sb.AppendLine($"  │  App: {x.AppName,-18} Typ: {x.TypeCode,4}  {x.Value}");
                sb.AppendLine("  └──────────────────────────────────────────");
            }

            sb.AppendLine();
            return sb.ToString();
        }
    }
}
