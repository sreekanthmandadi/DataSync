using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DataSync
{
    public class DataSync
    {
        private readonly HttpClient _client;
        private JsonMediaTypeFormatter _formatter;

        public DataSync(HttpClient client)
        {
            _client = client;
            _formatter = new JsonMediaTypeFormatter();
            _formatter.SerializerSettings = new JsonSerializerSettings();
            _formatter.SerializerSettings.Converters.Add(new StringEnumConverter());
        }

        public string GetToken()
        {
            var loginReq = new Dictionary<string, string>
            {
                { "instanceName", "AuditProdigy" },
                { "UserName", "syncap" },
                { "Password", "Password@1"},
                { "UserDomain",""}
            };

            HttpResponseMessage response = _client.PostAsync<Dictionary<string,string>>("core/security/login", loginReq, _formatter).Result;
            response.EnsureSuccessStatusCode();

            var result = response.Content.ReadAsAsync<SessionRootObject>().Result;
            
            return result.RequestedObject.SessionToken;
        }

        public int UpdateDatasyncJob(int? id, string token, DateTime? startDateTime, DateTime? endDateTime)
        {
            return SQLAdapter.UpdateDataSync("Marathon", id, token, startDateTime, endDateTime);

            //List<SqlParameter> sqlParamters = new List<SqlParameter>() {
            //        new SqlParameter("@Id",id),
            //        new SqlParameter("@Token",token),
            //        new SqlParameter("@StartDate", startDateTime),
            //        new SqlParameter("@EndDate", endDateTime)
            //    };

            //SQLAdapter.ExecuteSPWithReturnValue("Marathon", "rsa.UpdateDatasyncJob", sqlParamters);
        }

        public List<UserRootObject> SyncUsers(int jobId, string token)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Archer", "session-id="+token);

            HttpResponseMessage response = _client.GetAsync("core/system/user").Result;
            response.EnsureSuccessStatusCode();
            //var resultStr = response.Content.ReadAsStringAsync().Result;
            var result = response.Content.ReadAsAsync<List<UserRootObject>>().Result;
            List<UserRootObject> lastupdatedUsers = new List<UserRootObject>();

            //get only updated users since the job last run date & time
            var jobLastRuNDate = SQLAdapter.GetDatasyncLastRunDateTime(jobId);

            if (jobLastRuNDate != null && jobLastRuNDate != DateTime.MinValue)
            {
                if(result.Any(a => a.RequestedObject.UpdateInformation.UpdateDate >= jobLastRuNDate))
                {
                    //lastupdatedUsers = (List<UserRootObject>)result.Where(a => a.RequestedObject.UpdateInformation.UpdateDate >= jobLastRuNDate);
                    lastupdatedUsers =  result.Where(a => a.RequestedObject.UpdateInformation.UpdateDate >= jobLastRuNDate).ToList<UserRootObject>();
                }       
            }
            else
                lastupdatedUsers = result;

            foreach (UserRootObject user in lastupdatedUsers)
            {
                List<SqlParameter> sqlParamters = new List<SqlParameter>() {
                    new SqlParameter("@Id",user.RequestedObject.Id),
                    new SqlParameter("@FirstName",user.RequestedObject.FirstName),
                    new SqlParameter("@LastName",user.RequestedObject.LastName),
                    new SqlParameter("@Company",user.RequestedObject.Company != null? user.RequestedObject.Company: ""),
                    new SqlParameter("@Title",user.RequestedObject.Title != null? user.RequestedObject.Title: "")
                };

                SQLAdapter.ExecuteSPWithoutReturnValue("Marathon", "rsa.SyncResource", sqlParamters);
            }

            //SyncUserContacts

            //foreach (UserRootObject user in result)
            //{
            //    List<SqlParameter> sqlParamters = new List<SqlParameter>() {
            //        new SqlParameter("@Id",user.RequestedObject.Id),
            //        new SqlParameter("@FirstName",user.RequestedObject.FirstName),
            //        new SqlParameter("@LastName",user.RequestedObject.LastName),
            //        new SqlParameter("@Company",user.RequestedObject.Company != null? user.RequestedObject.Company: ""),
            //        new SqlParameter("@Title",user.RequestedObject.Title != null? user.RequestedObject.Title: "")                    
            //    };

            //    SQLAdapter.ExecuteSPWithoutReturnValue("Marathon", "rsa.SyncResource", sqlParamters);
            //}            

            return lastupdatedUsers;
        }

        public void syncUserContacts(string token, List<UserRootObject> userList)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Archer", "session-id=" + token);

            HttpResponseMessage response = _client.GetAsync("core/system/usercontact").Result;
            response.EnsureSuccessStatusCode();
            var resultStr = response.Content.ReadAsStringAsync().Result;
            var result = response.Content.ReadAsAsync<List<UserContactRootObject>>().Result;

            string Password = "default1";
            string salt = "afsdafshtyyukuiyergrsgsasfgh";
            string PassHash = "";

            MD5 md5Hash = MD5.Create();
            PassHash = GetMd5Hash(md5Hash, salt + Password);

            //Get only updated users list from SyncUsers and update only those contacts
            HashSet<int> commonUsers = new HashSet<int>(userList.Select(s => s.RequestedObject.Id));
            var results = result.Where(m => commonUsers.Contains(m.RequestedObject.UserId)).ToList();

            foreach (UserContactRootObject user in results)
            {
                string email = null, phone = null;

                if (user.RequestedObject.Contacts != null && user.RequestedObject.Contacts.Count >0)
                {
                    foreach(var contact in user.RequestedObject.Contacts)
                    {
                        if(contact.ContactType == 7 && contact.ContactSubType ==2)
                        {
                            email = contact.Value;
                        }
                        else if (contact.ContactType == 9 && contact.ContactSubType == 9)
                        {
                            phone = contact.Value;
                        }
                    }
                }

                List<SqlParameter> sqlParamters = new List<SqlParameter>() {
                    new SqlParameter("@Id",user.RequestedObject.UserId),
                    new SqlParameter("@Email", email),
                    new SqlParameter("@Phone", phone)
                };

                SQLAdapter.ExecuteSPWithoutReturnValue("Marathon", "rsa.SyncResourceContacts", sqlParamters);
            }

        }

        public void syncManagerAccess(string token, List<UserRootObject> userList)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Archer", "session-id=" + token);

            HttpResponseMessage response = _client.GetAsync("core/system/rolemembership").Result;
            response.EnsureSuccessStatusCode();
            var resultStr = response.Content.ReadAsStringAsync().Result;
            var result = response.Content.ReadAsAsync<List<UserAccessRootObject>>().Result;

            //Get only updated users list from SyncUsers and update only those contacts
            //HashSet<int> commonUsers = new HashSet<int>(userList.Select(s => s.RequestedObject.Id));
            //var results = result.Where(m => commonUsers.Contains(m.RequestedObject.UserIds)).ToList();

            foreach (UserAccessRootObject user in result.Where((u) => u.RequestedObject.RoleId ==74))
            {
                if (user.RequestedObject.UserIds !=  null && user.RequestedObject.UserIds.Count > 0)
                {
                    foreach (var id in user.RequestedObject.UserIds)
                    {
                        List<SqlParameter> sqlParamters = new List<SqlParameter>() {
                            new SqlParameter("@Id",id)
                        };

                        SQLAdapter.ExecuteSPWithoutReturnValue("Marathon", "rsa.SyncManagerAccess", sqlParamters);
                    }
                }

                
            }

        }

        static string GetMd5Hash(MD5 md5Hash, string input)
        {

            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }
    }

    public class UserConfig
    {
        public string TimeZoneId { get; set; }
        public int TimeZoneIdSource { get; set; }
        public string LocaleId { get; set; }
        public int LocaleIdSource { get; set; }
        public int LanguageId { get; set; }
        public int DefaultHomeDashboardId { get; set; }
        public int DefaultHomeWorkspaceId { get; set; }
        public int LanguageIdSource { get; set; }
        public int PlatformLanguageId { get; set; }
        public string PlatformLanguagePath { get; set; }
        public int PlatformLanguageIdSource { get; set; }
    }

    public class SessionObject
    {
        public string SessionToken { get; set; }
        public string InstanceName { get; set; }
        public int UserId { get; set; }
        public int ContextType { get; set; }
        public UserConfig UserConfig { get; set; }
        public bool Translate { get; set; }
        public bool IsAuthenticatedViaRestApi { get; set; }
    }

    public class SessionRootObject
    {
        public List<object> Links { get; set; }
        public SessionObject RequestedObject { get; set; }
        public bool IsSuccessful { get; set; }
        public List<object> ValidationMessages { get; set; }
    }



    public class UpdateInformation
    {
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
        public int CreateLogin { get; set; }
        public int UpdateLogin { get; set; }
    }

    public class UserObject
    {
        public int Id { get; set; }
        public string DisplayName { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public string UserName { get; set; }
        public int AccountStatus { get; set; }
        public int? DomainId { get; set; }
        public int SecurityId { get; set; }
        public string Locale { get; set; }
        public string TimeZoneId { get; set; }
        public string Address { get; set; }
        public string Company { get; set; }
        public string Title { get; set; }
        public object AdditionalNote { get; set; }
        public object BusinessUnit { get; set; }
        public object Department { get; set; }
        public bool ForcePasswordChange { get; set; }
        public object DistinguishedName { get; set; }
        public int Type { get; set; }
        public int? LanguageId { get; set; }
        public int? DefaultHomeDashboardId { get; set; }
        public int? DefaultHomeWorkspaceId { get; set; }
        public UpdateInformation UpdateInformation { get; set; }
    }

    public class UserRootObject
    {
        public List<object> Links { get; set; }
        public UserObject RequestedObject { get; set; }
        public bool IsSuccessful { get; set; }
        public List<object> ValidationMessages { get; set; }
    }


    public class Contact
    {
        public int ContactType { get; set; }
        public int ContactSubType { get; set; }
        public bool IsDefault { get; set; }
        public string Value { get; set; }
        public int Id { get; set; }
    }

    public class UserContactObject
    {
        public int UserId { get; set; }
        public List<Contact> Contacts { get; set; }
    }

    public class UserContactRootObject
    {
        public List<object> Links { get; set; }
        public UserContactObject RequestedObject { get; set; }
        public bool IsSuccessful { get; set; }
        public List<object> ValidationMessages { get; set; }
    }


    public class UserAccessObject
    {
        public int RoleId { get; set; }
        public List<int> UserIds { get; set; }
        public List<int> GroupIds { get; set; }
    }

    public class UserAccessRootObject
    {
        public List<object> Links { get; set; }
        public UserAccessObject RequestedObject { get; set; }
        public bool IsSuccessful { get; set; }
        public List<object> ValidationMessages { get; set; }
    }


}
