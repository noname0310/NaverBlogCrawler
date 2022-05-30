using NaverBlogCrawler;
using OpenQA.Selenium.Firefox;

Console.OutputEncoding = System.Text.Encoding.UTF8;

const int maxTextLength = 3000;

var driverOptions = new FirefoxOptions();
driverOptions.AddArgument("--headless");
var driver = new FirefoxDriver(driverOptions);
var controller = new DriverController(driver);

using var file = new StreamWriter("trainset.csv");
var dataCount = 0;

var query = Console.ReadLine()!;
var searchResults = controller.SearchFromNaverBlog(query);
foreach (var searchResult in searchResults)
{
    var result = controller.GetPostBody(searchResult.Url);
    if (result == null) continue;

    Console.Clear();
    Console.WriteLine($"Title: {searchResult.Title}");
    Console.WriteLine(result);
    processInput:
    Console.WriteLine($"Current Data Count: {dataCount}");
    Console.Write("is trusted text? (y/n/s/q): ");
    var input = Console.ReadLine();
    if (input is not {Length: > 0}) goto processInput;
    var trusted = input[0];
    if (trusted != 'y' && trusted != 'n' && trusted != 's' && trusted != 'q') goto processInput;

    if (trusted == 's') continue;
    if (trusted == 'q') break;

    var splicedResult =
        result
            .Replace("\n", " ")
            .Replace('\"', '\'')
            .Split('.');

    foreach (var line in splicedResult)
    {
        switch (line.Length)
        {
            case < 5:
            case > maxTextLength:
                continue;
            default:
                file.WriteLine($"\"{line}\", {(trusted == 'y' ? "true" : "false")}");
                dataCount += 1;
                break;
        }
    }
}
driver.Quit();
