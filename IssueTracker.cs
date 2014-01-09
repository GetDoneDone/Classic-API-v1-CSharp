using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Web;
using System.Security.Cryptography;
using System.IO;

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

        public APIException(string message, HttpWebResponse resp) : base(message)
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

        #region Public constructor methods

        /// <summary>
        /// Public default constructor
        /// </summary>
        /// <param name="subdomain">Subdomain of DoneDone account (e.g. mycompany.mydonedone.com -> subdomain = mycompany)</param>
        /// <param name="username">DoneDone username</param>
        /// <param name="passwordOrAPIToken">DoneDone password or API Token</param>
        public IssueTracker(string subdomain, string username, string passwordOrAPIToken)
        {
            auth = Convert.ToBase64String(Encoding.Default.GetBytes(string.Format("{0}:{1}", username, passwordOrAPIToken)));
            baseURL = string.Format("https://{0}.mydonedone.com/IssueTracker/API/", subdomain);
        }

        /// <summary>
        /// retaining for backwards compatibility
        /// </summary>
        /// <param name="subdomain">Subdomain of DoneDone account (e.g. mycompany.mydonedone.com -> subdomain = mycompany)</param>
        /// <param name="APItoken">API token</param>
        /// <param name="username">DoneDone username</param>
        /// <param name="password">DoneDone password</param>
        public IssueTracker(string subdomain, string APItoken, string username, string password)
        {
            if (!string.IsNullOrWhiteSpace(APItoken))
            {
                auth = Convert.ToBase64String(Encoding.Default.GetBytes(string.Format("{0}:{1}", username, APItoken)));
            }
            else
            {
                auth = Convert.ToBase64String(Encoding.Default.GetBytes(string.Format("{0}:{1}", username, password)));
            }

            baseURL = string.Format("https://{0}.mydonedone.com/IssueTracker/API/", subdomain);
        }

        #endregion

        #region Private helper methods

        /// <summary>
        /// Get mime type for a file
        /// </summary>
        /// <param name="Filename">file name</param>
        /// <returns></returns>
        private string getMimeType(string Filename)
        {
            string mime = "application/octetstream";
            string ext = Path.GetExtension(Filename).ToLower();
            Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);

            if (rk != null && rk.GetValue("Content Type") != null)
            {
                mime = rk.GetValue("Content Type").ToString();
            }
            
            return mime;
        }

        /// <summary>
        /// Perform generic API calling
        /// </summary>
        /// <param name="methodURL">IssueTracker method URL</param>
        /// <param name="data">Generic data</param>
        /// <param name="attachments">List of file paths (optional)</param>
        /// <param name="update">flag to indicate if this is a  PUT operation</param>
        /// <returns>the JSON string returned from server</returns>
        private string api(string methodURL, List<KeyValuePair<string, string>> data, List<string> attachments, bool update)
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
                        postParams.Add(String.Format("{0}={1}", item.Key, Uri.EscapeUriString(item.Value)));
                    }

                    string postQuery = String.Join("&", postParams.ToArray());
                    formData = Encoding.UTF8.GetBytes(postQuery);
                    request.ContentLength = formData.Length;

                    using (var requestStream = request.GetRequestStream())
                    {
                        requestStream.Write(formData, 0, formData.Length);
                        requestStream.Flush();
                    }
                }
                else
                {
                    var boundary = "------------------------" + DateTime.Now.Ticks;
                    var newLine = Environment.NewLine;

                    request.ContentType = "multipart/form-data; boundary=" + boundary;

                    using (var requestStream = request.GetRequestStream())
                    {
                        #region Stream data to request

                        var fieldTemplate = newLine + "--" + boundary + newLine + "Content-Type: text/plain" + 
                            newLine + "Content-Disposition: form-data;name=\"{0}\"" + newLine + newLine + "{1}";

                        var fieldData = "";

                        foreach (KeyValuePair<string, string> item in data)
                        {
                            fieldData += String.Format(fieldTemplate, item.Key, item.Value);
                        }

                        var fieldBytes = Encoding.UTF8.GetBytes(fieldData);
                        requestStream.Write(fieldBytes, 0, fieldBytes.Length);

                        #endregion

                        #region Stream files to request

                        var fileInfoTemplate = newLine + "--" + boundary + newLine + "Content-Disposition: filename=\"{0}\"" + 
                            newLine + "Content-Type: {1}" + newLine + newLine;

                        foreach (var path in attachments)
                        {
                            using (var reader = new BinaryReader(File.OpenRead(path)))
                            {
                                #region Stream file info

                                var fileName = Path.GetFileName(path);
                                var fileInfoData = String.Format(fileInfoTemplate, fileName, getMimeType(fileName));
                                var fileInfoBytes = Encoding.UTF8.GetBytes(fileInfoData);

                                requestStream.Write(fileInfoBytes, 0, fileInfoBytes.Length);

                                #endregion 

                                #region Stream file

                                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                                {
                                    byte[] buffer = new byte[4096];
                                    var fileBytesRead = 0;

                                    while ((fileBytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                                    {
                                        requestStream.Write(buffer, 0, fileBytesRead);
                                    }
                                }

                                #endregion
                            }
                        }

                        var trailer = Encoding.ASCII.GetBytes(newLine + "--" + boundary + "--");
                        requestStream.Write(trailer, 0, trailer.Length);

                        #endregion
                    }
                }
            }

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(responseStream))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
            catch (WebException wex)
            {
                var response = (HttpWebResponse)wex.Response;

                string message = "An API Error occurred.";

                if (response == null)
                {
                    throw new APIException(message, null);
                }

                var code = response.StatusCode;

                using (Stream responseStream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        message = reader.ReadToEnd();
                    }
                }

                throw new APIException(message, response);
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        #endregion

        #region Public API wrapper methods

        /// <summary>
        /// Get projects
        /// http://www.getdonedone.com/api/#all-projects
        /// </summary>
        /// <param name="loadWithIssues">Passing true will deep load all of the projects as well as all of their active issues.</param>
        /// <returns>JSON string</returns>
        public string GetProjects(bool loadWithIssues = false)
        {
            string url = loadWithIssues ? "Projects/true" : "Projects";
            return api(url, null, null, false);
        }

        /// <summary>
        /// Get priority levels
        /// http://www.getdonedone.com/api/#get-priority-levels
        /// </summary>
        /// <returns>JSON string</returns>
        public string GetPriorityLevels()
        {
            return api("PriorityLevels", null, null, false);
        }

        /// <summary>
        /// Get all people in a project
        /// http://www.getdonedone.com/api/#get-people-in-project
        /// </summary>
        /// <param name="projectID">ID of project</param>
        /// <returns>JSON string</returns>
        public string GetAllPeopleInProject(int projectID)
        {
            return api("PeopleInProject/" + projectID, null, null, false);
        }

        /// <summary>
        /// Get all issues in a project
        /// http://www.getdonedone.com/api/#issues-in-project
        /// </summary>
        /// <param name="projectID">ID of project</param>
        /// <returns>JSON string</returns>
        public string GetAllIssuesInProject(int projectID)
        {
            return api("IssuesInProject/" + projectID, null, null, false);
        }

        /// <summary>
        /// Check if an issue exists
        /// http://www.getdonedone.com/api/#checking-issue-existence
        /// </summary>
        /// <param name="projectID">ID of project</param>
        /// <param name="issueID">ID of issue</param>
        /// <returns>JSON string</returns>
        public string DoesIssueExist(int projectID, int issueID)
        {
            return api("DoesIssueExist/" + projectID + "/" + issueID, null, null, false);
        }

        /// <summary>
        /// Get all potential statuses for issue
        /// http://www.getdonedone.com/api/#allowing-issue-statuses
        /// </summary>
        /// <param name="projectID">ID of project</param>
        /// <param name="issueID">ID of issue</param>
        /// <returns>JSON string</returns>
        public string GetPotentialStatusesForIssue(int projectID, int issueID)
        {
            return api("PotentialStatusesForIssue/" + projectID + "/" + issueID, null, null, false);
        }

        /// <summary>
        /// Get issue details
        /// http://www.getdonedone.com/api/#issue-details
        /// </summary>
        /// <param name="projectID">ID of project</param>
        /// <param name="issueID">ID of issue</param>
        /// <returns>JSON string</returns>
        public string GetIssueDetails(int projectID, int issueID)
        {
            return api("Issue/" + projectID + "/" + issueID, null, null, false);
        }

        /// <summary>
        /// Get people who can be assigned to an issue
        /// http://www.getdonedone.com/api/#reassignment-permissions
        /// </summary>
        /// <param name="projectID">ID of project</param>
        /// <param name="issueID">ID of issue</param>
        /// <returns>JSON string</returns>
        public string GetPeopleForIssueAssignment(int projectID, int issueID)
        {
            return api("PeopleForIssueAssignment/" + projectID + "/" + issueID, null, null, false);
        }

        /// <summary>
        /// Create Issue
        /// http://www.getdonedone.com/api/#creating-issues
        /// </summary>
        /// <param name="projectID">ID of project</param>
        /// <param name="title">Title of issue (required)</param>
        /// <param name="priorityLevelID">Priority level ID of issue</param>
        /// <param name="resolverID">ID of person assigned to fix the issue</param>
        /// <param name="testerID">ID of person assigned to test the issue</param>
        /// <param name="description">Description</param>
        /// <param name="tags">A list of tags</param>
        /// <param name="peopleToCCIDs">A list of people to be cc'd on the issue</param>
        /// <param name="dueDate">Date the issue is due</param>
        /// <param name="attachments">List of file paths</param>
        /// <returns>JSON string</returns>
        public string CreateIssue(
            int projectID, 
            string title, 
            short priorityLevelID,
            int resolverID, 
            int testerID, 
            string description = null,
            List<string> tags = null,
            List<int> peopleToCCIDs = null, 
            DateTime? dueDate = null, 
            List<string> attachments = null)
        {
            var data = new List<KeyValuePair<string, string>>();

            data.Add(new KeyValuePair<string, string>("title", title));
            data.Add(new KeyValuePair<string, string>("priority_level_id", priorityLevelID.ToString()));
            data.Add(new KeyValuePair<string, string>("resolver_id", resolverID.ToString()));
            data.Add(new KeyValuePair<string, string>("tester_id", testerID.ToString()));

            if (description != null)
            {
                data.Add(new KeyValuePair<string, string>("description", description));
            }

            if (tags != null)
            {
                data.Add(new KeyValuePair<string, string>("tags", String.Join(",", tags)));
            }

            if (peopleToCCIDs != null)
            {
                data.Add(new KeyValuePair<string, string>("watcher_ids", String.Join(",", peopleToCCIDs)));
            }

            if (dueDate != null)
            {
                data.Add(new KeyValuePair<string, string>("due_date", dueDate.ToString()));
            }

            return api("Issue/" + projectID, data, attachments, false);
        }

        /// <summary>
        /// Create comment for an issue 
        /// http://www.getdonedone.com/api/#creating-comments
        /// </summary>
        /// <param name="projectID">ID of project</param>
        /// <param name="issueID">ID of issue</param>
        /// <param name="comment">Comment</param>
        /// <param name="peopleToCCIDs">A list of people to be cc'd on the issue</param>
        /// <param name="attachments">List of file paths</param>
        /// <returns>JSON string</returns>
        public string CreateComment(
            int projectID,
            int issueID, 
            string comment,
            List<int> peopleToCCIDs = null, 
            List<string> attachments = null)
        {
            var data = new List<KeyValuePair<string, string>>();

            data.Add(new KeyValuePair<string, string>("comment", comment));

            if (peopleToCCIDs != null)
            {
                data.Add(new KeyValuePair<string, string>("people_to_cc_ids", String.Join(",", peopleToCCIDs)));
            }

            return api("Comment/" + projectID + "/" + issueID, data, attachments, false);
        }

        /// <summary>
        /// Update issue
        /// http://www.getdonedone.com/api/#updating-issues
        /// </summary>
        /// <param name="projectID">ID of project</param>
        /// <param name="issueID">ID of issue</param>
        /// <param name="title">title</param>
        /// <param name="priorityLevelID">Priority level ID of issue</param>
        /// <param name="resolverID">ID of person assigned to fix the issue</param>
        /// <param name="testerID">ID of person assigned to test the issue</param>
        /// <param name="description">Description</param>
        /// <param name="tags">A list of tags (Note: if blank, existing tags will be removed from issue)</param>
        /// <param name="statusID">ID of valid issue statues for issue</param>
        /// <param name="dueDate">Due date (Note: if blank, due date will be removed from issue)</param>
        /// <returns>JSON string</returns>
        public string UpdateIssue(int projectID, 
            int issueID, 
            string title = null, 
            short? priorityLevelID = null, 
            int? resolverID = null,
            int? testerID = null, 
            string description = null,
            string tags = null, 
            short? statusID = null,
            DateTime? dueDate = null)
        {
            var data = new List<KeyValuePair<string, string>>();

            if (title != null)
            {
                data.Add(new KeyValuePair<string, string>("title", title));
            }

            if (priorityLevelID.HasValue)
            {
                data.Add(new KeyValuePair<string, string>("priority_level_id", priorityLevelID.Value.ToString()));
            }

            if (resolverID.HasValue)
            {
                data.Add(new KeyValuePair<string, string>("resolver_id", resolverID.Value.ToString()));
            }

            if (testerID.HasValue)
            {
                data.Add(new KeyValuePair<string, string>("tester_id", testerID.Value.ToString()));
            }

            if (description != null)
            {
                data.Add(new KeyValuePair<string, string>("description", description));
            }

            if (tags != null)
            {
                data.Add(new KeyValuePair<string, string>("tags", tags));
            }

            if (statusID.HasValue)
            {
                data.Add(new KeyValuePair<string, string>("state_id", statusID.Value.ToString()));
            }

            if (dueDate.HasValue)
            {
                data.Add(new KeyValuePair<string, string>("due_date", dueDate.Value.ToString()));
            }

            return api("Issue/" + projectID + "/" + issueID, data, null, true);
        }

        #endregion

    }
}
