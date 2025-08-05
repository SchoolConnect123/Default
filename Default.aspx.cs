using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;

public class OlympiadStudentRecord1
{
    public string student_name { get; set; }
    public string class_section { get; set; }
    public string school_name { get; set; }
    public string city { get; set; }
    public string country { get; set; }
    public string mobile_no { get; set; }
    public string school_contact { get; set; }
    public string student_email { get; set; }
    public string school_email { get; set; }
    public string olympiad_id { get; set; }
}

public partial class _Default : Page
{
    private readonly string connstr = ConfigurationManager.ConnectionStrings["scoConn"].ConnectionString;

    protected async void Page_Load(object sender, EventArgs e)
    {
        // Async image list
        await ImageListAsync();

        if (!IsPostBack)
        {
            ClearSessionKeys();
            var tasks = new Task[]
            {
                LoadDashboardCountersAsync(),
                BindOlympiadAsync(),
                // BindExamAsync removed: no ddlExam control
                BindPowerPackPackagesAsync(),
                BindClassAsync()
            };
            await Task.WhenAll(tasks);
        }
    }

    #region Async Data-Bind Helpers

    private async Task ImageListAsync()
    {
        var dt = new DataTable();
        using (var conn = new SqlConnection(connstr))
        using (var cmd = new SqlCommand("SELECT * FROM GalleryMaster", conn))
        {
            await conn.OpenAsync();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                dt.Load(reader);
            }
        }
        rptGalleryName.DataSource = dt;
        rptGalleryName.DataBind();
    }

    private async Task LoadDashboardCountersAsync()
    {
        var dsLocal = new DataSet();
        using (var conn = new SqlConnection(connstr))
        using (var cmd = new SqlCommand(@"
            SELECT c.courseID,c.coursename,ISNULL(cm.topicCount,0) AS topicCount,(ISNULL(cm.topicCount,0)*5) AS videoCount,
                   ISNULL(csm.subjectCount,0) AS subjectCount,ISNULL(q.questionCount,0) AS questionCount
            FROM CourseMaster c
            LEFT JOIN (SELECT CourseID,COUNT(*) AS topicCount FROM CourseMaterialMaster GROUP BY CourseID) cm ON cm.CourseID=c.courseID
            LEFT JOIN (SELECT CourseID,COUNT(*) AS subjectCount FROM CourseSubjectMapping GROUP BY CourseID) csm ON csm.CourseID=c.courseID
            LEFT JOIN (SELECT CourseID,COUNT(*) AS questionCount FROM QuestionMaster GROUP BY CourseID) q ON q.CourseID=c.courseID
            ORDER BY c.courseCode;
            SELECT examname,examicon,examcode FROM exammaster ORDER BY examname;
            SELECT DISTINCT esm.examid,csm.CourseID,e.examcode FROM ExamSubjectMapping esm
            JOIN CourseSubjectMapping csm ON csm.coursesubjectmapid=esm.coursesubjectmapid
            JOIN ExamMaster e ON e.examID=esm.examID;", conn))
        {
            await conn.OpenAsync();
            using (var adapter = new SqlDataAdapter(cmd)) adapter.Fill(dsLocal);
        }
        rptClassCounter.DataSource = dsLocal.Tables[0];
        rptClassCounter.DataBind();
        rptExams.DataSource = dsLocal.Tables[1];
        rptExams.DataBind();

        foreach (RepeaterItem item in rptClassCounter.Items)
        {
            var ltrl = (Literal)item.FindControl("ltrlExams");
            var hfcid = (HiddenField)item.FindControl("hfcid");
            dsLocal.Tables[2].DefaultView.RowFilter = string.Format("courseID='{0}'", hfcid.Value);
            var filtered = dsLocal.Tables[2].DefaultView.ToTable();
            if (filtered.Rows.Count > 0)
            {
                var codes = string.Join(", ", filtered.AsEnumerable().Select(r => r["examcode"].ToString()));
                ltrl.Text = "like " + codes;
            }
            else
            {
                ltrl.Text = string.Empty;
            }
        }
    }

    private async Task BindOlympiadAsync()
    {
        var dt = new DataTable();
        string sql = @"
            SELECT
                OlympiadName + ' (INR ' + CONVERT(VARCHAR(50), PriceINR) + '/USD ' + CONVERT(VARCHAR(50), PriceUSD) + ')' AS OlympiadName,
                OlympiadID + ':' + CONVERT(VARCHAR(50), PriceINR) + ':' + CONVERT(VARCHAR(50), PriceUSD) + ':' + OlympiadName AS OlympiadData
            FROM OlympiadMaster";
        using (var conn = new SqlConnection(connstr))
        using (var cmd = new SqlCommand(sql, conn))
        {
            await conn.OpenAsync();
            using (var reader = await cmd.ExecuteReaderAsync()) dt.Load(reader);
        }
        ddlOlympiad.AppendDataBoundItems = true;
        ddlOlympiad.DataSource = dt;
        ddlOlympiad.DataTextField = "OlympiadName";
        ddlOlympiad.DataValueField = "OlympiadData";
        ddlOlympiad.DataBind();
    }

    private async Task BindPowerPackPackagesAsync()
    {
        var dt = new DataTable();
        using (var conn = new SqlConnection(connstr))
        using (var cmd = new SqlCommand(@"
            SELECT
                Name + ' (INR ' + CONVERT(VARCHAR(50), OG_ProductPriceINR) + '/USD ' +
                CONVERT(VARCHAR(50),OG_ProductPriceUSD) + ')' AS ProductName,
                OG_ProductId + ':' + CONVERT(VARCHAR(50), OG_ProductPriceINR) + ':' +
                CONVERT(VARCHAR(50), OG_ProductPriceUSD) + ':' + Name AS ProductData
            FROM OG_Product", conn))
        {
            await conn.OpenAsync();
            using (var reader = await cmd.ExecuteReaderAsync()) dt.Load(reader);
        }
        ddlPowerPack.AppendDataBoundItems = true;
        ddlPowerPack.DataSource = dt;
        ddlPowerPack.DataTextField = "ProductName";
        ddlPowerPack.DataValueField = "ProductData";
        ddlPowerPack.DataBind();
    }

    private async Task BindClassAsync()
    {
        var dt = new DataTable();
        using (var conn = new SqlConnection(connstr))
        using (var cmd = new SqlCommand(@"
            SELECT DISTINCT c.courseID, c.courseName, CAST(c.courseCode AS INT) AS courseClass
            FROM dbo.orgCourseSubjectMapping ocsm
            JOIN dbo.CourseSubjectMapping csm ON csm.CourseSubjectMapID = ocsm.CourseSubjectMapID
            JOIN dbo.CourseMaster c ON c.courseID = csm.CourseID
            JOIN dbo.SectionMaster s ON c.courseID = s.courseID
            WHERE ocsm.orgID = @orgID AND s.IsOlympiad = 'True'
            ORDER BY courseClass", conn))
        {
            cmd.Parameters.AddWithValue("@orgID", ConfigurationManager.AppSettings["DefaultOlmpdOrg"]);
            await conn.OpenAsync();
            using (var reader = await cmd.ExecuteReaderAsync()) dt.Load(reader);
        }
        ddlCourse.Items.Clear();
        ddlCourse.DataSource = dt;
        ddlCourse.DataTextField = "courseName";
        ddlCourse.DataValueField = "courseID";
        ddlCourse.DataBind();
    }

    #endregion Async Data-Bind Helpers

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
        // TODO: Move your registration logic here
    }

    // Other existing methods (social login, MakeWebRequest, CreateUserAccount, etc.) unchanged

    #endregion  // End of Session & Helper Methods
}
