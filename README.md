# DoneDone API C# Client Library

This a C# wrapper to the DoneDone API

## Requirements
.Net 4 or above

## API Usage

You must enable the "Web Service API" for each DoneDone project you want to be accessible via the API.  Just go to the "Project Settings" page of a project, to enable the API. See http://www.getdonedone.com/api for more detailed documentation.

There is currently a rate limit of 1000 requests per hour per account.

## Example setup
```C#
class Program
    {
        static void Main(string[] args)
        {
            string subdomain = "acmecorp"; // The subdomain of your account (e.g. acmecorp.mydonedone.com)
	    string username = "username";
	    string apiToken = "XXXXXXXXXXXXXXXXXXXX"; // This can be found under your "View Profile" page in DoneDone

            var issueTrackerApi = new DoneDone.IssueTracker(subdomain, username, apiToken);

            try
            {
	 	// Call to get API-enabled projects in account. Returns JSON string of projects
                string availableProjects = issueTrackerApi.GetProjects(); 
                Console.WriteLine(p);
            }
            catch (DoneDone.APIException apie)
            {
                if (apie.Response != null)
                {
                     Console.WriteLine(string.Format("\r\nERROR\r\nCode:{0}\r\n{1}\r\n", 
   			apie.Response.StatusCode, apie.Message));
                       
		     if (apie.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
                     {
                          int retry_after = int.Parse(apie.Response.GetResponseHeader("Retry-After"));
		          Console.WriteLine("Retry after {0} seconds", retry_after);
                     }
                }
                else
                {
                     Console.WriteLine("Internal server error"); 
                }
            }
            
            Console.ReadKey();
        }
```
