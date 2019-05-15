using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace TestProject.Data
{
    /*
        Create a table table using this generic class. Only supports top level properties in class to create from. So no deep nested properties.

        Usage Example:

        using (SqlConnection connection = new SqlConnection("MyConnectionString"))
        {
            connection.Open();

            using (SqlTransaction trans = connection.BeginTransaction())
            {
                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, trans))
                {
                    DataTable dt = dataTableService.MakeDataTable(DataEntityClass, null, bulkCopy);
                    bulkCopy.DestinationTableName = "TableToInsertTo";
                    bulkCopy.WriteToServer(dt);
                }

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, trans))
                {
                    DataTable dt = dataTableService.MakeDataTable(DataEntityClass2, null, bulkCopy);
                    bulkCopy.DestinationTableName = "TableToInsertTo2";
                    bulkCopy.WriteToServer(dt);
                }
                //.............
                trans.Commit();
            }
        }
     */
    public class DataTableService
    {
        public DataTable MakeDataTable<T>(IEnumerable<T> entities, List<string> propertiesToIgnore = null, SqlBulkCopy sqlBulkCopyToAddColumnMappings = null)
        {
            List<ColumnDef> columnDefs = GetColumnDefinitions(typeof(T), propertiesToIgnore);

            DataTable dt = new DataTable(typeof(T).Name);
            columnDefs.ForEach((c) => {
                dt.Columns.Add(c.ColumnName, c.ColumnType);
            });

            foreach (T e in entities)
            {
                DataRow row = dt.NewRow();

                columnDefs.ForEach((c) => {
                    var value = typeof(T).GetProperty(c.ColumnName).GetValue(e, null);
                    if (value != null)
                    {
                        row[c.ColumnName] = value;
                    }
                });
                dt.Rows.Add(row);
            }

            if (sqlBulkCopyToAddColumnMappings != null)
            {
                columnDefs.ForEach((c) =>
                {
                    sqlBulkCopyToAddColumnMappings.ColumnMappings.Add(new SqlBulkCopyColumnMapping(c.ColumnName, c.ColumnName));
                });
            }

            return dt;
        }

        protected List<ColumnDef> GetColumnDefinitions(Type type, List<string> propertiesToIgnore)
        {
            List<ColumnDef> columns = new List<ColumnDef>();

            var fdProps = type.GetProperties();
            foreach (var prop in fdProps)
            {
                if (propertiesToIgnore != null && propertiesToIgnore.Contains(prop.Name.ToUpper()))
                {
                    continue;
                }

                // If nullable get underlying Type
                if (Nullable.GetUnderlyingType(prop.PropertyType) != null)
                {
                    columns.Add(new ColumnDef(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType)));
                }
                else
                {
                    columns.Add(new ColumnDef(prop.Name, prop.PropertyType));
                }
            }

            return columns;
        }

        protected class ColumnDef
        {
            public ColumnDef(string columName, Type columnType)
            {
                ColumnName = columName;
                ColumnType = columnType;
            }

            public string ColumnName { get; set; }
            public Type ColumnType { get; set; }
        }
    }
}
