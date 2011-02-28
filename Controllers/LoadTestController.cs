using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Net;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace LoadTest.Controllers
{
	public class LoadTestController : Controller
	{
		//
		// GET: /LoadTest/

		public ActionResult Index()
		{
			ViewBag.Url = "http://";
			ViewBag.Hits = "10";
			return View();
		}

		[HttpPost]
		public ActionResult Run(string url, int? hits, int? rmax, string method)
		{
			if (!Regex.IsMatch(url, @"^http(s)?://.+"))
				ModelState.AddModelError("url", "That looks like a malformed URL (Did you put in the http?)");
			if (ModelState.IsValid) {
				bool useRandomNumber = rmax.HasValue && url.Contains("{rand}");
				List<double> totals = new List<double>();
				DateTime start = DateTime.Now;
				List<int> status = new List<int>();
				Parallel.For(0, hits ?? 10, (i) => {
					//put some random numbers in!
					Random r = new Random(i + DateTime.Now.Millisecond);
					string testUrl = url;
					if (useRandomNumber) {
						testUrl = url.ToString().Replace("{rand}", r.Next(rmax ?? 1).ToString());
					}
					//create request
					HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(testUrl);
					req.Timeout = 30000; // 30 seconds. we don't have all day.
					req.Method = method == "POST" ? method : "GET";
					DateTime t = DateTime.Now;
					HttpWebResponse resp;
					try {
						using (resp = (HttpWebResponse)req.GetResponse()) { status.Add((int)resp.StatusCode); }
					} catch (WebException e) {
						if (e.Response != null)
							using (resp = (HttpWebResponse)e.Response) {
								status.Add((int)resp.StatusCode);
							}
						else
							status.Add(0);
					}

					//done
					double total = DateTime.Now.Subtract(t).TotalMilliseconds;
					totals.Add(total);
				});
				//we don't want too many results in the graph. 50-100 is plenty.
				int mod = totals.Count / 50;
				mod = mod < 1 ? 1 : mod;
				double max = totals.Max();
				var statusCount = status.GroupBy(i => i).ToDictionary(g => g.Key, g => g.Count() * 100 / hits);
				return View(new TestResults() {
					Hits = hits ?? 10,
					AverageResponseTime = totals.Average(),
					TotalTime = DateTime.Now.Subtract(start).TotalSeconds,
					ResponseChartData = string.Format("chs=400x150&cht=lc&chd=t:{0}&chxt=x,y&chxl=0:||1:|0|{1}ms", string.Join(",", totals.Where((e, i) => i % mod == 0).Select(v => (v * 100 / max).ToString("0"))), max.ToString("0")),
					StatusChartData = string.Format("cht=p3&chd=t:{0}&chs=400x150&chl={1}", string.Join(",", statusCount.Select(s => s.Value)), string.Join("|", statusCount.Select(s => s.Key)))
				});
			}
			return View("Index");
		}

	}

	public class TestResults
	{
		public double Hits { get; set; }
		public double TotalTime { get; set; }
		public double AverageResponseTime { get; set; }
		public string ResponseChartData { get; set; }
		public string StatusChartData { get; set; }
	}
}
