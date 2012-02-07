# DoneDone API C# Client Library

## REQUIREMENT
C# version 3.5, 4 (developed against 4)

## USAGE
To use the C# library with a DoneDone project, you will need to enable the API option under the Project Settings page.

Please see http://www.getdonedone.com/api fore more detailed documentation.

## EXAMPLES
```C#
/// Initializing
using DoneDone;

var domain = "YOUR_COMPANY_DOMAIN"; ///e.g. wearemammoth 
var token = "YOUR_API_TOKEN";
var username = "YOUR_USERNAME";
var password = "YOUR_PASSWORD";

var issueTracker = new IssueTracker(domain, token, username, password);

///
/// Calling the API 
///
/// API methods can be accessed by calling IssueTracker::API(), or by calling the equivalent shorthand.
///
/// The examples below will get all your projects with the API enabled.
///
issueTracker.API("GetProjects");
/// or
issueTracker.GetProjects();
```