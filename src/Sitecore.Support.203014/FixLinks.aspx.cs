namespace Sitecore.Support
{
  using Sitecore.Data;
  using Sitecore.Data.Fields;
  using Sitecore.Data.Items;
  using Sitecore.SecurityModel;
  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using System.Web;
  using System.Web.UI;
  using System.Web.UI.WebControls;

  public partial class FixLinks : System.Web.UI.Page
  {
    static bool running = false;
    static List<string> results = new List<string>();
    static int num = 0;
    protected void Page_Load(object sender, EventArgs e)
    {
      if (running)
      {
        this.Button1.Enabled = false;
        Response.Write("status: processing " + num + "<br/>");
        if (!Page.IsStartupScriptRegistered("refresh") && !IsPostBack)
        {
          string scriptBlock =
             @"<script language=""JavaScript"">
               setTimeout(function(){window.location.reload(1);},5000);
               </script>";


          Page.RegisterStartupScript("refresh", scriptBlock);
        }
      }
      else
      {
        Response.Write("status: idle " + num + "<br/>");
      }
      lock (results)
      {
        foreach (string s in results)
        {
          Response.Write(s + "<br/>");
        }
      }

    }

    protected void Button1_Click(object sender, EventArgs e)
    {
      running = true;
      num = 0;
      Task.Run(() => this.go());
      Response.Redirect(Request.RawUrl);
    }

    public void go()
    {
      try
      {

        using (new SecurityDisabler())
        {
          Database db = Database.GetDatabase("master");
          Item root = db.GetRootItem();
          Stack<Item> items = new Stack<Item>();
          items.Push(root);
          while (items.Count > 0)
          {
            Item current = items.Pop();
            foreach (Item specific in current.Versions.GetVersions(true))
            {
              foreach (Field f in specific.Fields)
              {
                if (f.Type == "General Link" || f.Type == "General Link with Search" || f.Type=="link")
                {
                  LinkField link = (LinkField)f;
                  if (!string.IsNullOrEmpty(f.GetValue(false, false)) && !string.IsNullOrEmpty(link.GetAttribute("linktype")) && link.IsInternal)
                  {
                    string id = link.GetAttribute("id");
                    ID target = Sitecore.Data.ID.Null;
                    if (!Sitecore.Data.ID.TryParse(id, out target))
                    {
                      using (new EditContext(specific))
                      {
                        specific.Fields[link.InnerField.ID].Value = string.Empty;
                      }
                      lock (results)
                      {
                        results.Add(specific.Uri.ToString() + " - " + link.InnerField.Name);

                      }

                    }

                  }
                }
              }
            }

            Interlocked.Increment(ref num);
            foreach (Item child in current.Children)
            {
              items.Push(child);
            }

          }
        }


      }
      catch (Exception e)
      {
        lock (results)
        {
          results.Add(e.Message);
          results.Add(e.StackTrace);
        }
      }
      finally
      {
        running = false;
      }
    }
  }
}