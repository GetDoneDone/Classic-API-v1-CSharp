using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Web;
using System.Security.Cryptography;


namespace DoneDone
{
    public sealed class APIException : Exception
    {
        private HttpWebResponse _response;
        public HttpWebResponse Response
        {
            get
            {
                return _response;
            }
        }
        public APIException(string message, HttpWebResponse resp)
            : base(message)
        {
            _response = resp;
        }
    }
    /// <summary>
    /// Provide access to the DoneDone IssueTracker API. 
    /// </summary>
    public class IssueTracker
    {
        protected string baseURL;
        protected string auth;
        protected string password;

        /// <summary>
        /// public default constructor
        /// 
        /// </summary>
        /// <param name="domain">company's DoneDone domain</param>
        /// <param name="username">DoneDone username</param>
        /// <param name="password_or_api_token">DoneDone password or API Token</param>
        public IssueTracker(string domain, string username, string password_or_api_token)
        {
            auth = Convert.ToBase64String(Encoding.Default.GetBytes(string.Format("{0}:{1}", username, password_or_api_token)));
            baseURL = string.Format("https://{0}.mydonedone.com/IssueTracker/API/", domain);
        }

        /// <summary>
        /// retaining for backwards compatibility
        /// </summary>
        /// <param name="domain">your domain</param>
        /// <param name="APItoken">your token</param>
        /// <param name="username">your username</param>
        /// <param name="password">your password</param>
        public IssueTracker(string domain, string APItoken, string username, string password)
        {
            if (!string.IsNullOrWhiteSpace(APItoken))
            {
                auth = Convert.ToBase64String(Encoding.Default.GetBytes(string.Format("{0}:{1}", username, APItoken)));
            }
            else
            {
                auth = Convert.ToBase64String(Encoding.Default.GetBytes(string.Format("{0}:{1}", username, password)));

            } 
            baseURL = string.Format("https://{0}.mydonedone.com/IssueTracker/API/", domain);
       
        }

        /// <summary>
        /// Get mime type
        /// </summary>
        /// <param name="Filename">file name</param>
        /// <returns></returns>
        private string getMimeType(string Filename)
        {
            string mime = "application/octetstream";
            string ext = System.IO.Path.GetExtension(Filename).ToLower();
            Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
            if (rk != null && rk.GetValue("Content Type") != null)
            {
                mime = rk.GetValue("Content Type").ToString();
            }
            return mime;
        }



        /// <summary>
        /// Perform generic API calling
        /// 
        /// This is the base method for all IssueTracker API calls.
        /// </summary>
        /// <param name="methodURL">IssueTracker method URL</param>
        /// <param name="data">optional POST form data</param>
        /// <param name="attachments">optional list of file paths</param>
        /// <param name="update">flag to indicate if this is a PUT operation</param>
        /// <returns>the JSON string returned from server</returns>
        private string api(string methodURL,
            List<KeyValuePair<string, string>> data,
            List<string> attachments, bool update)
        {
            string url = baseURL + methodURL;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Headers.Add("Authorization: Basic " + auth);

            if (data == null && attachments == null)
            {
                request.Method = "GET";
            }
            else
            {
                request.Method = update ? "PUT" : "POST";
                byte[] formData = null;

                if (attachments == null)
                {
                    request.ContentType = "application/x-www-form-urlencoded";
                    var postParams = new List<string>();
                    foreach (KeyValuePair<string, string> item in data)
                    {
                        postParams.Add(
                            String.Format("{0}={1}", item.Key, System.Web.HttpUtility.UrlEncode(item.Value)));
                    }
                    string postQuery = String.Join("&", postParams.ToArray());
                    formData = Encoding.UTF8.GetBytes(postQuery);
                    request.ContentLength = formData.Length;

                    using (System.IO.Stream requestStream = request.GetRequestStream())
                    {
                        requestStream.Write(formData, 0, formData.Length);
                        requestStream.Flush();
                        requestStream.Close();
                    }
                }
                else
                {
                    string boundary = "------------------------" + DateTime.Now.Ticks;
                    request.ContentType = "multipart/form-data; boundary=" + boundary;
                    var newLine = Environment.NewLine;
                    string fileName = "";

                    string formatedData = "";
                    var fieldTemplate = newLine + "--" + boundary + newLine +
                        "Content-Type: text/plain" + newLine +
                        "Content-Disposition: form-data;name=\"{0}\"" +
                        newLine + newLine + "{1}";
                    foreach (KeyValuePair<string, string> item in data)
                    {
                        formatedData +=
                            String.Format(fieldTemplate, item.Key, item.Value);
                    }
                    byte[] bytes = { };
                    StringBuilder stringBuilder = new StringBuilder();
                    var fileTemplate = newLine + "--" + boundary + newLine +
                        "Content-Disposition: filename=\"{0}\"" +
                        newLine + "Content-Type: {1}" + newLine + newLine;
                    foreach (var path in attachments)
                    {
                        bytes = System.IO.File.ReadAllBytes(path);
                        fileName = System.IO.Path.GetFileName(path);
                        formatedData +=
                            String.Format(fileTemplate, fileName, getMimeType(fileName));
                        formatedData += Convert.ToBase64String(bytes);

                    }
                    formatedData += newLine + "--" + boundary + "--";
                    request.ContentLength = formatedData.Length;
                    using (var reqStream = request.GetRequestStream())
                    using (var reqWriter = new System.IO.StreamWriter(reqStream))
                    {
                        reqWriter.Write(formatedData);
                        reqWriter.Flush();
                    }
                }
            }
            try
            {
                // Get the response.
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {

                    using (System.IO.Stream responseStream = response.GetResponseStream())
                    {
                        using (System.IO.StreamReader reader = new System.IO.StreamReader(responseStream))
                        {

                            return reader.ReadToEnd();

                        }
                    }
                }
            }
            catch (WebException we)
            {
                var response = (HttpWebResponse)we.Response;

                string message = "An API Error occurred.";
                if (response == null)
                {
                    throw new APIException(message, null);
                }
                var code = response.StatusCode;
                using (System.IO.Stream responseStream = response.GetResponseStream())
                {
                    using (System.IO.StreamReader reader = new System.IO.StreamReader(responseStream))
                    {

                        message = reader.ReadToEnd();

                    }
                }
                throw new APIException(message, response);
            }
            catch (APIException)
            {
                throw;
            }
            catch (Exception e)
            {

                return e.Message;
            }
        }

        /// <summary>
        /// Get all Projects with the API enabled
        /// </summary>
        /// <param name="loadWithIssues">Passing true will deep load all of the projects as well as all of their active issues.</param>
        /// <returns></returns>
        public string GetProjects(bool loadWithIssues = false)
        {
            string url = loadWithIssues ? "Projects/true" : "Projects";
            return api(url, null, null, false);
        }


        /// <summary>
        /// Get priority levels
        /// </summary>
        /// <returns>the JSON string returned from server</returns>
        public string GetPriorityLevels()
        {
            return api("PriorityLevels", null, null, false);
        }

        /// <summary>
        /// Get all people in a project
        /// </summary>
        /// <param name="projectID">project id</param>
        /// <returns>the JSON string returned from server</returns>
        public string GetAllPeopleInProject(string projectID)
        {
            return api("PeopleInProject/" + projectID, null, null, false);
        }

        /// <summary>
        /// Get all issues in a project
        /// </summary>
        /// <param name="projectID">project id</param>
        /// <returns>the JSON string returned from server</returns>
        public string GetAllIssuesInProject(string projectID)
        {
            return api("IssuesInProject/" + projectID, null, null, false);
        }

        /// <summary>
        /// Check if an issue exists
        /// </summary>
        /// <param name="projectID">project id</param>
        /// <param name="issueID">issue id</param>
        /// <returns>the JSON string returned from server</returns>
        public string DoesIssueExist(string projectID, string issueID)
        {
            return api("DoesIssueExist/" + projectID + "/" + issueID, null, null, false);
        }

        /// <summary>
        /// Get all potential statuses for issue
        /// 
        /// Note: If you are an admin, you"ll get both all allowed statuses 
        /// as well as ALL statuses back from the server
        /// </summary>
        /// <param name="projectID">project id</param>
        /// <param name="issueID">issue id</param>
        /// <returns>the JSON string returned from server</returns>
        public string GetPotentialStatusesForIssue(string projectID, string issueID)
        {
            return api("PotentialStatusesForIssue/" + projectID + "/" + issueID, null, null, false);
        }

        /// <summary>
        /// Note: You can use this to check if an issue exists as well, 
        /// since it will return a 404 if the issue does not exist.
        /// </summary>
        /// <param name="projectID">project id</param>
        /// <param name="issueID">issue id</param>
        /// <returns></returns>
        public string GetIssueDetails(string projectID, string issueID)
        {
            return api("Issue/" + projectID + "/" + issueID, null, null, false);
        }

        /// <summary>
        /// Get a list of people that cane be assigend to an issue
        /// </summary>
        /// <param name="projectID">project id</param>
        /// <param name="issueID">issue id</param>
        /// <returns>the JSON string returned from server</returns>
        public string GetPeopleForIssueAssignment(string projectID, string issueID)
        {
            return api("PeopleForIssueAssignment/" + projectID + "/" + issueID, null, null, false);
        }

        /// <summary>
        /// Create Issue
        /// </summary>
        /// <param name="projectID">project id</param>
        /// <param name="title">required title</param>
        /// <param name="priorityID">priority levels</param>
        /// <param name="resolverID">person assigned to solve this issue</param>
        /// <param name="testerID">person assigned to test and verify if a issue is resolved</param>
        /// <param name="description">optional description of the issue.</param>
        /// <param name="tags">a string of tags delimited by comma.</param>
        /// <param name="watcherIDs">a string of people"s id delimited by comma.</param>
        /// <param name="attachments">list of file paths.</param>
        /// <returns>the JSON string returned from server</returns>
        public string CreateIssue(
            string projectID, string title, string priorityID,
            string resolverID, string testerID, string description = null,
            string tags = null, string watcherIDs = null, List<string> attachments = null)
        {
            var data = new List<KeyValuePair<string, string>>();
            data.Add(new KeyValuePair<string, string>("title", title));
            data.Add(new KeyValuePair<string, string>("priority_level_id", priorityID));
            data.Add(new KeyValuePair<string, string>("resolver_id", resolverID));
            data.Add(new KeyValuePair<string, string>("tester_id", testerID));

            if (description != null)
            {
                data.Add(new KeyValuePair<string, string>("description", description));
            }
            if (tags != null)
            {
                data.Add(new KeyValuePair<string, string>("tags", tags));
            }
            if (watcherIDs != null)
            {
                data.Add(new KeyValuePair<string, string>("watcher_ids", watcherIDs));
            }
            return api("Issue/" + projectID, data, attachments, false);
        }

        /// <summary>
        /// Create Comment on issue
        /// </summary>
        /// <param name="projectID">project id</param>
        /// <param name="issueID">issue id</param>
        /// <param name="comment">comment string</param>
        /// <param name="peopleToCCID">a string of people to be CCed on this comment, delimited by comma.</param>
        /// <param name="attachments">list of file paths.</param>
        /// <returns>the JSON string returned from server</returns>
        public string CreateComment(
            string projectID, string issueID, string comment,
            string peopleToCCID = null, List<string> attachments = null)
        {
            var data = new List<KeyValuePair<string, string>>();
            data.Add(new KeyValuePair<string, string>("comment", comment));

            if (peopleToCCID != null)
            {
                data.Add(new KeyValuePair<string, string>("people_to_cc_ids", peopleToCCID));
            }

            return api("Comment/" + projectID + "/" + issueID, data, attachments, false);
        }

        /// <summary>
        /// Update issue.
        /// 
        /// If you provide any parameters then the value you pass will be 
        /// used to update the issue. If you wish to keep the value that's 
        /// already on an issue, then do not provide the parameter in your 
        /// PUT data. Any value you provide, including tags, will overwrite 
        /// the existing values on the issue. If you wish to retain the tags 
        /// for an issue and update it by adding one new tag, then you"ll have 
        /// to provide all of the existing tags as well as the new tag in your 
        /// tags parameter, for example.
        /// </summary>
        /// <param name="projectID">project id</param>
        /// <param name="issueID">issue id</param>
        /// <param name="title">required title</param>
        /// <param name="priorityID">priority levels</param>
        /// <param name="resolverID">person assigned to solve this issue</param>
        /// <param name="testerID">person assigned to test and verify if a issue is resolved.</param>
        /// <param name="description">optional description of the issue.</param>
        /// <param name="tags">a string of tags delimited by comma.</param>
        /// <param name="stateID">a valid state that this issue can transition to</param>
        /// <param name="attachments">list of file paths</param>
        /// <returns>the JSON string returned from server</returns>
        public string UpdateIssue(
            string projectID, string issueID, string title = null,
            string priorityID = null, string resolverID = null,
            string testerID = null, string description = null,
            string tags = null, string stateID = null,
            List<string> attachments = null)
        {
            var data = new List<KeyValuePair<string, string>>();
            if (title != null)
            {
                data.Add(new KeyValuePair<string, string>("title", title));
            }
            if (priorityID != null)
            {
                data.Add(new KeyValuePair<string, string>("priority_level_id", priorityID));
            }
            if (resolverID != null)
            {
                data.Add(new KeyValuePair<string, string>("resolver_id", resolverID));
            }
            if (testerID != null)
            {
                data.Add(new KeyValuePair<string, string>("tester_id", testerID));
            }
            if (description != null)
            {
                data.Add(new KeyValuePair<string, string>("description", description));
            }
            if (tags != null)
            {
                data.Add(new KeyValuePair<string, string>("tags", tags));
            }
            if (stateID != null)
            {
                data.Add(new KeyValuePair<string, string>("state_id", stateID));
            }
            return api("Issue/" + projectID + "/" + issueID, data, attachments, true);
        }

    }
}
