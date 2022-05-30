using System.Net;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace NaverBlogCrawler;

internal record struct SearchItem(string Title, string Url);

internal class DriverController
{
    private readonly WebDriver _driver;
    private readonly string _searchWindowHandle;

    public DriverController(WebDriver driver)
    {
        _driver = driver;
        _searchWindowHandle = driver.CurrentWindowHandle;
    }

    public IReadOnlyList<SearchItem> SearchFromNaverBlog(string query)
    {
        SwitchToSearchTab();
        
        var encodedQuery = WebUtility.UrlEncode(query);
        _driver.Navigate().GoToUrl($"https://search.naver.com/search.naver?where=view&sm=tab_jum&query={encodedQuery}");
        WaitUntilPageLoaded();

        //scroll down
        {
            var lastHeight = (long)_driver.ExecuteScript("return document.body.scrollHeight");

            for (var i = 0; i < 10; i++)
            {
                _driver.ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                Thread.Sleep(2000);
                var scrollHeight = (long)_driver.ExecuteScript("return document.body.scrollHeight");
                if (lastHeight == scrollHeight) break;
                lastHeight = scrollHeight;
            }
        }

        return ScanSearchResult();
    }

    private IReadOnlyList<SearchItem> ScanSearchResult()
    {
        SwitchToSearchTab();

        var resultList = new List<SearchItem>();
        var webElement = _driver.FindElements(By.CssSelector("li[class='bx _svp_item'] div[class='total_area']"));
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var item in webElement)
        {
            var docTitleElement = item.FindElement(By.CssSelector("a[class='api_txt_lines total_tit _cross_trigger']"));
            var title = docTitleElement.Text;
            var docUrl = docTitleElement.GetAttribute("href");
            resultList.Add(new SearchItem(title, docUrl));
        }

        return resultList;
    }

    public string? GetPostBody(string postUrl)
    {
        //create a new tab
        _driver.ExecuteScript($"window.open('{postUrl}', '_blank');");
        _driver.SwitchTo().Window(_driver.WindowHandles.Last());

        WaitUntilPageLoaded();
        Thread.Sleep(1000);

        //enter iframe

        if (_driver.FindElements(By.Id("mainFrame")).Count > 0)
        {
            _driver.SwitchTo().Frame("mainFrame");
        }
        else if (_driver.FindElements(By.Id("screenFrame")).Count > 0)
        {
            _driver.SwitchTo().Frame("screenFrame");
            _driver.SwitchTo().Frame("mainFrame");
        }
        else if (_driver.FindElements(By.Id("cafe_main")).Count > 0)
        {
            _driver.SwitchTo().Frame("cafe_main");
        }

        string? result = null;
        
        if (TryFindElementFromMultipleSelector(
                new[]
                {
                    "div[id='postViewArea']",
                    "div[class='se-viewer se-theme-default']",
                    "div[class='se-section se-section-text se-l-default']",
                    "div[class='se-main-container']",
                    "div[class='se_component_wrap sect_dsc __se_component_area']",
                    "div[class='article_viewer']"
                },
                out var bodyElement
            ))
        {
            result = ((string) _driver.ExecuteScript(
                    //====================================JavaScript=====================================
                    "    var itemEle = arguments[0].cloneNode(true);                                 " +
                    "    var removeElements = itemEle.getElementsByClassName(\"se_mediaArea\");      " +
                    "    while (removeElements.length > 0) {                                         " +
                    "        removeElements[0].parentNode.removeChild(removeElements[0]);            " +
                    "    }                                                                           " +
                    "    var item = itemEle.innerHTML;                                               " +
                    "    var item = item.replace(/<br>/ig, \"|n\");                                  " +
                    "    var item = item.replace(/(<([^>]+)>)/ig,\"\");                              " +
                    "    itemEle.remove();                                                           " +
                    "    return item;                                                                ",
                    //===================================================================================
                    bodyElement))
                .ReplaceLineEndings()
                .Replace("|n", "\n")
                .Replace("&nbsp;", " ");

            result = Regex.Replace(result, @"\s+", " ");
        }

        //leave iframe
        _driver.SwitchTo().DefaultContent();

        //close tab
        _driver.ExecuteScript("window.close();");
        _driver.SwitchTo().Window(_searchWindowHandle); 
            
        return result;
    }

    private bool TryFindElementFromMultipleSelector(IEnumerable<string> selectors, out IWebElement element)
    {
        IWebElement? resultElement = null;

        foreach (var selector in selectors)
        {
            try
            {
                resultElement = _driver.FindElement(By.CssSelector(selector));
            }
            catch (NoSuchElementException)
            {
                continue;
            }

            break;
        }

        element = resultElement!;
        return resultElement != null;
    }

    private void WaitUntilPageLoaded(int timeOut = 50)
    {
        new WebDriverWait(_driver, new TimeSpan(0, 0, timeOut)).Until(
            d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState") as string == "complete");
    }

    private void SwitchToSearchTab()
    {
        if (_driver.CurrentWindowHandle == _searchWindowHandle) return;
        _driver.SwitchTo().Window(_searchWindowHandle);
    }
}
