using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataSync
{
    public class SQLAdapter
    {
        public static bool ExecuteSPWithoutReturnValue(string client, string storedProcName, List<SqlParameter> parameters)
        {
            int rows;
            using (SqlConnection con = new SqlConnection())
            {
                con.ConnectionString = ConfigurationManager.ConnectionStrings[string.Format("{0}{1}", client, "DBConnection")].ConnectionString;
                using (SqlCommand cmd = new SqlCommand(storedProcName, con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    if (parameters != null)
                    {
                        foreach (SqlParameter p in parameters)
                        {
                            cmd.Parameters.AddWithValue(p.ParameterName, p.Value);
                        }
                    }
                    try
                    {
                        con.Open();
                        rows = cmd.ExecuteNonQuery();
                    }
                    catch
                    {
                        if (cmd != null)
                        {
                            cmd.Dispose();
                        }
                        if (con != null)
                        {
                            con.Close();
                            con.Dispose();
                        }
                        throw;
                    }
                    finally { con.Close(); }
                }
            }
            return rows > 0;
        }

        public static int UpdateDataSync(string client, int? id, string token, DateTime? StartDate, DateTime? EndDate)
        {
            int returnValue;
            using (SqlConnection con = new SqlConnection())
            {
                con.ConnectionString = ConfigurationManager.ConnectionStrings[string.Format("{0}{1}", client, "DBConnection")].ConnectionString;
                string query = "Insert into [rsa].[DatasyncJob] (Token, StartDate) Values ('" + token + "','" + StartDate + "'); Select @@IDENTITY";

                if (id != null && EndDate != null)
                {
                    query = "Update [rsa].[DatasyncJob] Set EndDate = '" + EndDate + "' Where Id = " + id;
                }

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.CommandType = CommandType.Text;
                    
                    try
                    {
                        con.Open();
                        returnValue = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    catch(Exception ex)
                    {
                        if (cmd != null)
                        {
                            cmd.Dispose();
                        }
                        if (con != null)
                        {
                            con.Close();
                            con.Dispose();
                        }

                        throw ex;
                    }
                    finally { con.Close(); }
                }
            }
            return returnValue;
        }

        public static DateTime GetDatasyncLastRunDateTime(int jobId)
        {
            DateTime returnValue = DateTime.MinValue;

            using (SqlConnection con = new SqlConnection())
            {
                con.ConnectionString = ConfigurationManager.ConnectionStrings[string.Format("{0}{1}", "Marathon", "DBConnection")].ConnectionString;
                string query = "SELECT top 1 StartDate,EndDate from [rsa].[DatasyncJob] Where Id < " + jobId + " order by 1 desc";
                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.CommandType = CommandType.Text;
                    con.Open();

                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        if (!string.IsNullOrEmpty(reader[1].ToString()))
                            returnValue = Convert.ToDateTime(reader[1]);
                    }
                }
            }

            return returnValue;
        }
    }
}
