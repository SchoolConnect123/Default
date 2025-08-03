using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

public class OlympiadStudentRecord1
{
    public string student_name   { get; set; }
    public string class_section  { get; set; }
    public string school_name    { get; set; }
    public string city           { get; set; }
    public string country        { get; set; }
    public string mobile_no      { get; set; }
    public string school_contact { get; set; }
    public string student_email  { get; set; }
    public string school_email   { get; set; }
    public string olympiad_id    { get; set; }
}

public partial class _Default : Page
{
    private readonly string connstr = ConfigurationManager
        .ConnectionStrings["scoConn"].ConnectionString;

    protected async void Page_Load(object sender, EventArgs e)
    {
        await ImageListAsync();

        if (!IsPostBack)
        {
            ClearSessionKeys();
            var tasks = new Task[]
            {
                LoadDashboardCountersAsync(),
                BindOlympiadAsync(),
                BindPowerPackPackagesAsync(),  // you can remove this call too if you never use PowerPack
                BindClassAsync()
            };
            await Task.WhenAll(tasks);
        }
    }

    // … your existing Async Data-Bind Helpers omitted for brevity …

    #region Session & Helper Methods

    private void ClearSessionKeys()
    {
        var keys = new[]
        {
            "olympiad_id","olympiad_name","order_id","olympiad_currency",
            "olympiad_email","olympiad_amount","olympiad_student",
            "olympiad_studentid","powerpacks_status","powerpack_olympiad_id",
            "powerpack_olympiad_name","powerpack_olympiad_amount",
            "olmpowerpacks_status","Power_Pack_Amount"
        };
        foreach (var key in keys) Session.Remove(key);
    }

    protected void btnRegister_Click(object sender, EventArgs e)
    {
        // 1) Read inputs
        string name      = txtName.Text.Trim();
        string school    = txtSchool.Text.Trim();
        string country   = txtCountry.Text.Trim();
        string mobile    = txtMoblile.Text.Trim();
        string email     = txtEmail.Text.Trim();
        string courseId  = ddlCourse.SelectedValue;
        string courseName= ddlCourse.SelectedItem.Text;
        string currency  = Request.Form["currency"]?.ToUpper() ?? "USD";
        string amount    = txtPrice.Text.Trim();

        // Selected Olympiads
        var selectedList = ddlOlympiad.Items.Cast<ListItem>()
                             .Where(li => li.Selected)
                             .Select(li => li.Value)
                             .ToArray();
        if (selectedList.Length == 0)
        {
            lblMessage.Text = "Please select at least one Olympiad and click OK.";
            lblMessage.CssClass = "alert-danger olympiad-alert";
            return;
        }

        // 2) Generate IDs
        var indiaTZ = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        var now     = TimeZoneInfo.ConvertTime(DateTime.Now, indiaTZ);
        string studentId = "OLMPD-STU" + now.ToString("ddMMyyyy")
                           + Guid.NewGuid().ToString("N");
        string orderId   = "ORD-" + Guid.NewGuid().ToString("N");

        // 3) Log signup
        try
        {
            var record = new OlympiadStudentRecord1
            {
                student_name   = name,
                class_section  = courseId,
                school_name    = school,
                city           = "",
                country        = country,
                mobile_no      = mobile,
                school_contact = "",
                student_email  = email,
                school_email   = "",
                olympiad_id    = string.Join(",", selectedList)
            };
            string detailsJson = JsonConvert.SerializeObject(record);

            using (var conn = new SqlConnection(connstr))
            using (var cmd  = new SqlCommand(
                "INSERT INTO OlympiadSignupLog(StudentDetails,CreateDate,lastaccessIP) " +
                "VALUES(@d,@dt,@ip)", conn))
            {
                cmd.Parameters.AddWithValue("@d",  detailsJson);
                cmd.Parameters.AddWithValue("@dt", now);
                cmd.Parameters.AddWithValue("@ip", Request.UserHostAddress);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
        catch
        {
            // swallow or log as needed
        }

        // 4) Save main student record
        bool saved = false;
        using (var conn = new SqlConnection(connstr))
        using (var cmd  = new SqlCommand(
          "INSERT INTO OlympiadStudent(" +
          "StudentId, StudentName, SchoolName, Class_Section, City, Country, " +
          "MobileNo, SchoolContact, StudentEmail, SchoolEmail, SelectedOlympiads, " +
          "RegistrationDate, ExamDate, PaymentStatus, IsEmailVerified) " +
          "VALUES(@id,@name,@school,@cls,'',@country,@mob,'',@semail,'',@selols," +
          "@rdate,'','Unpaid','False')", conn))
        {
            cmd.Parameters.AddWithValue("@id",      studentId);
            cmd.Parameters.AddWithValue("@name",    name);
            cmd.Parameters.AddWithValue("@school",  school);
            cmd.Parameters.AddWithValue("@cls",     courseId);
            cmd.Parameters.AddWithValue("@country", country);
            cmd.Parameters.AddWithValue("@mob",     mobile);
            cmd.Parameters.AddWithValue("@semail",  email);
            cmd.Parameters.AddWithValue("@selols",  string.Join(",", selectedList));
            cmd.Parameters.AddWithValue("@rdate",   now);

            conn.Open();
            saved = cmd.ExecuteNonQuery() > 0;
        }

        if (!saved)
        {
            lblMessage.Text = "Registration failed. Please try again.";
            lblMessage.CssClass = "alert-danger olympiad-alert";
            return;
        }

        // 5) Store in Session & redirect
        Session["olympiad_studentid"] = studentId;
        Session["order_id"]           = orderId;
        Session["olympiad_currency"]  = currency;
        Session["olympiad_amount"]    = amount;
        Session["olympiad_student"]   = name;
        Session["olympiad_email"]     = email;
        Session["course_id"]          = courseId;
        Session["course_name"]        = courseName;
        Session["olympiad_id"]        = selectedList;

        Response.Redirect("SelectOlympiadExamDate.aspx", false);
    }

    #endregion
}
