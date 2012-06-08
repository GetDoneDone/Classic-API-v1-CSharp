# DoneDone API C# Client Library

## REQUIREMENT
.net 4

## USAGE
To use the C# library with a DoneDone project, you will need to enable the API option under the Project Settings page.

Please see http://www.getdonedone.com/api fore more detailed documentation.

## EXAMPLES
```C#
class Program
    {
        static void Main(string[] args)
        {
            string DOMAIN = "your_domain";//your donedone account subdomain - so "your_domain" if your account URL is your_domain.mydonedone.com 
	    string USERNAME = "your_username";
	    string API_TOKEN = "your_token";

            var it = new DoneDone.IssueTracker(DOMAIN, USERNAME, API_TOKEN);
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    string p = it.GetProjects();
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
                            int retry_after = int.Parse(
						apie.Response.GetResponseHeader("Retry-After"));

                            Console.WriteLine("Retry after {0} seconds", retry_after);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Internal server error"); 
                    }
                }
            }
            Console.ReadKey();
        }
```
