using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

// Reference to AEC libraries
using Autodesk.Aec.Arch.DatabaseServices;

public class SpaceCommands
{
    [CommandMethod("ListRoomNames")]
    public void ListRoomNames()
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Editor ed = doc.Editor;
        Database db = doc.Database;

        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

            foreach (ObjectId objId in btr)
            {
                Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                if (ent is AecSpace space)
                {
                    ed.WriteMessage($"\nSpace Name: {space.Name}");
                }
            }
            tr.Commit();
        }
    }
}
