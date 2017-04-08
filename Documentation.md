Blogger2BlogML requires .NET 4.0 (client profile). If you don't already have this installed you'll get an error when you try to run it. Download [.NET 4.0](http://www.microsoft.com/downloads/details.aspx?familyid=5765D7A8-7722-4888-A970-AC39B33FD8AB&displaylang=en) and then run Windows Update to get any critical patches. 

Blogger2BlogML is a command line tool that takes two parameters. The first is the ATOM format export from Blogger, the second is the path to create the BlogML file. For example:

Blogger2BlogML C:\Users\Me\Desktop\BloggerExport.xml C:\Users\Me\Desktop\MyBlogInBlogML.xml

The BlogML file will be overwritten if it already exists.

Blogger2BlogML sets an exit code of 0 if successful. Any other exit code indicates a fatal error.