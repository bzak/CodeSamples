using System;
using WebPerspective.Areas.Graph.Models;
using WebPerspective.Entities;

namespace WebPerspective.Areas.Graph.Services
{
    public class LayoutClause
    {
        public string LayoutKey { get; set; }
        public bool? Modify { get; set; }
        public bool? Browser { get; set; }
        public LayoutSettings Settings { get; set; } = new LayoutSettings();


        public void SetProperty(string key, string value)
        {
            if (key.ToLower() == "key")
            {
                this.LayoutKey = value;
            }
            else if (key.ToLower() == "modify")
            {
                this.Modify = value.ToLower() == "true";
            }
            else if (key.ToLower() == "browser")
            {
                this.Browser = value.ToLower() == "true";
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public PropertyGraphModel Transform(PropertyGraphModel result)
        {
            result.Data["layout"] = this;
            return result;
        }
    }
}