# DoneDone API C# Client Library

This a C# wrapper to the DoneDone API

## Requirements
.Net 4 or above

## API usage

You must enable the "Web Service API" for each DoneDone project you want to be accessible via the API.  Just go to the "Project Settings" page of a project, to enable the API. See http://www.getdonedone.com/api for more detailed documentation.

## Rate limiting

There is currently a rate limit of 1000 requests per hour per account.

## Help us make it better

One improvement we'd like is to have the API wrapper methods return C# objects instead of a JSON string.  We've got a lot cooking with DoneDone and just haven't gotten around to doing this.  Send us a pull request if you'd like to take this on and we'll kick you back a free month on your account!

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
                // Call to get API-enabled projects in account. Returns JSON string of projects.
                string availableProjects = issueTrackerApi.GetProjects(); 
                Console.WriteLine(availableProjects);
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
    }
```
