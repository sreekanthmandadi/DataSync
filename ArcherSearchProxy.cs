using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using ArcherSearch;


namespace DataSync
{
    public class ArcherSearchProxy
    {
        public void SyncProjects(string token)
        {

            //var authClient = new ArcherGeneral.generalSoapClient();
            //token = authClient.CreateUserSessionFromInstance("sysadmin", "AuditProdigy", "Password123!");           

            var client = new searchSoapClient("searchSoap");
            //var response = client.ExecuteQuickSearchWithModuleIds(token, "271", "EMEA", 1, 50);
            
            var searchOptions = @"<SearchReport><PageSize>100</PageSize>
                                <DisplayFields>
                                    <DisplayField>5686</DisplayField>
                                    <DisplayField>5689</DisplayField>
                                    <DisplayField>9908</DisplayField>
                                    <DisplayField>15498</DisplayField>
                                    <DisplayField>15520</DisplayField>
                                </DisplayFields>
                                <Criteria>
                                    <ModuleCriteria><Module>271</Module>
                                    <IsKeywordModule>False</IsKeywordModule>
                                    </ModuleCriteria>
                                </Criteria></SearchReport>";

            var searchResponse = client.ExecuteSearch(token, searchOptions, 1);

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(searchResponse);

            XmlNodeList nodes = doc.DocumentElement.SelectNodes("/Records/Record");

            foreach (XmlNode node in nodes)
            {
                List<SqlParameter> sqlParamters = new List<SqlParameter>() {
                    new SqlParameter("@Engagement_ID",node.ChildNodes[0].InnerText),
                    new SqlParameter("@Engagement_Name",node.ChildNodes[1].InnerText),
                    new SqlParameter("@Audit_Scope__Objectives",node.ChildNodes[2].InnerText),
                    new SqlParameter("@Projected_Hours",node.ChildNodes[3].InnerText == "" || node.ChildNodes[3].InnerText == "0" ? node.ChildNodes[4].InnerText: node.ChildNodes[3].InnerText )
                };

                SQLAdapter.ExecuteSPWithoutReturnValue("Marathon", "rsa.SyncProject", sqlParamters);
            }
        }
    }
}
