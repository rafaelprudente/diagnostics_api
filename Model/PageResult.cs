namespace doctors_api.Model
{
    public class PageResult<T> : PageResultBase where T : class
    {
        public IList<T> Results { get; set; }
        public PageResult() { Results = new List<T>(); }
    }
}
