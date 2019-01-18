using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DataSync
{
    class Program
    {
        private static readonly HttpClient _client = new HttpClient();        
        static void Main(string[] args)
        {
            _client.BaseAddress = new Uri("http://pe050.pe-lab.com/api/");

            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
            
            //while(1==1)
            //{
                DataSync ds = new DataSync(_client);

                var token = ds.GetToken();

                if (!string.IsNullOrEmpty(token))
                {
                    //JOB Start Date & Time
                    var id = ds.UpdateDatasyncJob(null, token, DateTime.Now, null);

                    var users = ds.SyncUsers(id, token);

                    ds.syncUserContacts(token, users);

                    ds.syncManagerAccess(token, users);

                    ArcherSearchProxy client = new ArcherSearchProxy();
                    client.SyncProjects(token);

                    //JOB END Date & Time
                    ds.UpdateDatasyncJob(id, token, null, DateTime.Now);
                }
            //}
        }
        
    }
}
