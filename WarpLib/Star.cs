using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Warp.Tools;

namespace Warp
{
    public class Star
    {
        Dictionary<string, int> NameMapping = new Dictionary<string, int>();
        List<List<string>> Rows = new List<List<string>>();

        public int RowCount => Rows.Count;

        public Star(string path, string tableName = "")
        {
            using (TextReader Reader = new StreamReader(File.OpenRead(path)))
            {
                string Line;

                if (!string.IsNullOrEmpty(tableName))
                    while ((Line = Reader.ReadLine()) != null && !Line.Contains(tableName)) ;

                while ((Line = Reader.ReadLine()) != null && !Line.Contains("loop_")) ;

                while (true)
                {
                    Line = Reader.ReadLine();

                    if (Line == null)
                        break;
                    if (Line.Length == 0)
                        continue;
                    if (Line[0] != '_')
                        break;

                    string[] Parts = Line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string ColumnName = Parts[0].Substring(1);
                    int ColumnIndex = int.Parse(Parts[1].Substring(1)) - 1;
                    NameMapping.Add(ColumnName, ColumnIndex);
                }

                do
                {
                    string[] Parts = Line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (Parts.Length == NameMapping.Count)
                        Rows.Add(new List<string>(Parts));
                    else
                        break;
                } while ((Line = Reader.ReadLine()) != null);
            }
        }

        public Star(string[] columnNames)
        {
            foreach (string name in columnNames)
                NameMapping.Add(name, NameMapping.Count);
        }

        public Star(Star[] tables)
        {
            List<string> Common = new List<string>(tables[0].GetColumnNames());

            foreach (var table in tables)
                Common.RemoveAll(c => !table.HasColumn(c));

            foreach (string name in Common)
                NameMapping.Add(name, NameMapping.Count);

            foreach (var table in tables)
            {
                int[] ColumnIndices = Common.Select(c => table.GetColumnID(c)).ToArray();

                for (int r = 0; r < table.RowCount; r++)
                {
                    List<string> Row = new List<string>(Common.Count);
                    for (int c = 0; c < ColumnIndices.Length; c++)
                        Row.Add(table.GetRowValue(r, ColumnIndices[c]));

                    AddRow(Row);
                }
            }
        }

        public void Save(string path)
        {
            using (TextWriter Writer = File.CreateText(path))
            {
                Writer.WriteLine("");
                Writer.WriteLine("data_");
                Writer.WriteLine("");
                Writer.WriteLine("loop_");

                foreach (var pair in NameMapping)
                    Writer.WriteLine($"_{pair.Key} #{pair.Value + 1}");

                foreach (var row in Rows)
                    Writer.WriteLine("  " + string.Join("  ", row));
            }
        }

        public string[] GetColumn(string name)
        {
            if (!NameMapping.ContainsKey(name))
                return null;

            int Index = NameMapping[name];
            string[] Column = new string[Rows.Count];
            for (int i = 0; i < Rows.Count; i++)
                Column[i] = Rows[i][Index];

            return Column;
        }

        public void SetColumn(string name, string[] values)
        {
            int Index = NameMapping[name];
            for (int i = 0; i < Rows.Count; i++)
                Rows[i][Index] = values[i];
        }

        public int GetColumnID(string name)
        {
            if (NameMapping.ContainsKey(name))
                return NameMapping[name];
            else
                return -1;
        }

        public string GetRowValue(int row, string column)
        {
            if (!NameMapping.ContainsKey(column))
                throw new Exception("Column does not exist.");
            if (row < 0 || row >= Rows.Count)
                throw new Exception("Row does not exist.");

            return GetRowValue(row, NameMapping[column]);
        }

        public string GetRowValue(int row, int column)
        {
            return Rows[row][column];
        }

        public void SetRowValue(int row, string column, string value)
        {
            Rows[row][NameMapping[column]] = value;
        }

        public bool HasColumn(string name)
        {
            return NameMapping.ContainsKey(name);
        }

        public void AddColumn(string name, string[] values)
        {
            int NewIndex = NameMapping.Select((v, k) => k).Max() + 1;
            NameMapping.Add(name, NewIndex);

            for (int i = 0; i < Rows.Count; i++)
                Rows[i].Insert(NewIndex, values[i]);
        }

        public void AddColumn(string name)
        {
            string[] EmptyValues = new string[Rows.Count];
            for (int i = 0; i < EmptyValues.Length; i++)
                EmptyValues[i] = "";

            AddColumn(name, EmptyValues);
        }

        public void RemoveColumn(string name)
        {
            int Index = NameMapping[name];
            foreach (List<string> row in Rows)
                row.RemoveAt(Index);

            NameMapping.Remove(name);
            var BiggerNames = NameMapping.Where(vk => vk.Value > Index).Select(vk => vk.Key).ToArray();
            foreach (var biggerName in BiggerNames)
                NameMapping[biggerName] = NameMapping[biggerName] - 1;

            var KeyValuePairs = NameMapping.Select(vk => vk).ToList();
            KeyValuePairs.Sort((vk1, vk2) => vk1.Value.CompareTo(vk2.Value));
            NameMapping = new Dictionary<string, int>();
            foreach (var keyValuePair in KeyValuePairs)
                NameMapping.Add(keyValuePair.Key, keyValuePair.Value);
        }

        public int GetColumnIndex(string name)
        {
            return NameMapping[name];
        }

        public string[] GetColumnNames()
        {
            return NameMapping.Select(pair => pair.Key).ToArray();
        }

        public List<string> GetRow(int index)
        {
            return Rows[index];
        }

        public void AddRow(List<string> row)
        {
            Rows.Add(row);
        }

        public void RemoveRows(int[] indices)
        {
            for (int i = indices.Length - 1; i >= 0; i--)
                Rows.RemoveAt(indices[i]);
        }
    }
}
