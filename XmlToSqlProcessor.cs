using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Data.SqlClient;



namespace WebService_Agrometal_Variantes
{
    public class XmlToSqlProcessor
    {
        private readonly string _connectionString;
        private readonly HashSet<string> _tablesCreated = new(StringComparer.OrdinalIgnoreCase);

        public XmlToSqlProcessor(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Process(string xmlPath)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                ProcessXml(xmlPath, conn);
            }
        }

        // ================================
        // Procesamiento principal XML
        // ================================
        private void ProcessXml(string xmlPath, SqlConnection conn)
        {
            using (XmlReader reader = XmlReader.Create(xmlPath))
            {
                while (reader.Read())
                {
                    if (IsRootElement(reader))
                    {
                        ProcessElement(reader, conn);
                    }
                }
            }
        }

        private bool IsRootElement(XmlReader reader)
        {
            return reader.NodeType == XmlNodeType.Element && reader.Depth == 1;
        }

        private void ProcessElement(XmlReader reader, SqlConnection conn)
        {
            string tableName = reader.Name;

            var values = new Dictionary<string, string>();

            ReadAttributes(reader, values);
            ReadChildElements(reader, values);

            CreateTableIfNotExists(tableName, values.Keys, conn);
            InsertRow(tableName, values, conn);
        }

        // ================================
        // Lectura XML
        // ================================
        private void ReadAttributes(XmlReader reader, Dictionary<string, string> values)
        {
            if (!reader.HasAttributes)
                return;

            while (reader.MoveToNextAttribute())
            {
                values[reader.Name] = reader.Value;
            }

            reader.MoveToElement();
        }

        private void ReadChildElements(XmlReader reader, Dictionary<string, string> values)
        {
            if (reader.IsEmptyElement)
                return;

            using (XmlReader subReader = reader.ReadSubtree())
            {
                subReader.Read();

                while (subReader.Read())
                {
                    if (subReader.NodeType == XmlNodeType.Element &&
                        subReader.Depth == 1 &&
                        subReader.HasAttributes)
                    {
                        string childName = subReader.Name;

                        while (subReader.MoveToNextAttribute())
                        {
                            string columnName = $"{childName}_{subReader.Name}";
                            values[columnName] = subReader.Value;
                        }

                        subReader.MoveToElement();
                    }
                }
            }
        }

        // ================================
        // Base de datos
        // ================================
        private void CreateTableIfNotExists(string tableName, IEnumerable<string> columns, SqlConnection conn)
        {
            // Primera vez en esta ejecución: drop + recreate con esquema NVARCHAR(MAX)
            // Las siguientes filas de la misma tabla solo insertan
            if (_tablesCreated.Contains(tableName)) return;
            _tablesCreated.Add(tableName);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"IF OBJECT_ID(N'[{tableName}]', N'U') IS NOT NULL DROP TABLE [{tableName}]");
            sb.AppendLine($"CREATE TABLE [{tableName}] (");

            foreach (var col in columns)
            {
                sb.AppendLine($"[{col}] NVARCHAR(MAX),");
            }

            sb.Length--;
            sb.AppendLine(")");

            using (SqlCommand cmd = new SqlCommand(sb.ToString(), conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertRow(string tableName, Dictionary<string, string> values, SqlConnection conn)
        {
            StringBuilder columns = new StringBuilder();
            StringBuilder parameters = new StringBuilder();

            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                foreach (var kvp in values)
                {
                    columns.Append($"[{kvp.Key}],");
                    parameters.Append($"@{kvp.Key},");

                    cmd.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? (object)DBNull.Value);
                }

                columns.Length--;
                parameters.Length--;

                cmd.CommandText = $@"
INSERT INTO [{tableName}] ({columns})
VALUES ({parameters})";

                cmd.ExecuteNonQuery();
            }
        }
    }
}
