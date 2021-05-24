using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using backend.Models;
using System.Data;
using System.Data.SqlClient;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Cors;
using System.IO;
using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Hosting;

namespace backend.Controllers
{
    [EnableCors("CorsApi")]
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _env;
        const string DB= "Server=tcp:quang.database.windows.net,1433;Initial Catalog=PYTHAGORAS;Persist Security Info=False;User ID=quang;Password=0917787421qQ;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        //############### Return the number list from db #######################
        [HttpGet]
        [Route("getNumber")]
        public string getNumber(){
            
            SqlConnection con= new SqlConnection(DB);
            con.Open();            
            string cmdText="Select * from Number";
            SqlCommand cmd=new SqlCommand(cmdText,con);
            SqlDataReader dataReader= cmd.ExecuteReader();
            DataTable dataTable=new DataTable();
            dataTable.Load(dataReader);
            con.Close();
            return JsonConvert.SerializeObject(dataTable);
        }

        //########################## Return feedback list from db ##########################
        [HttpGet]
        [Route("getFeedBack")]
        public string getFeedBack(){
            SqlConnection con= new SqlConnection(DB);
            con.Open();            
            string cmdText="Select * from FeedBack";
            SqlCommand cmd=new SqlCommand(cmdText,con);
            SqlDataReader dataReader= cmd.ExecuteReader();
            DataTable dataTable=new DataTable();
            dataTable.Load(dataReader);
            con.Close();
            return JsonConvert.SerializeObject(dataTable);
        }
        
        //############################# Add number information to db ###############################
        [HttpPost]
        [Route("addNumber")]
        public string addNumber(string id, string content, string imgURL)
        {
            content=content.Replace("%0A","\n");
            imgURL=imgURL.Replace("\\\\","/");
            imgURL=imgURL.Trim('/');
            SqlConnection con = new SqlConnection(DB);
            con.Open();
            string check_exist = String.Format("Select count(*) from Number where number={0}", id);
            SqlCommand cmd = new SqlCommand(check_exist, con);
            int exist = (int)cmd.ExecuteScalar();
            int status;
            if (exist > 0)
            {
                string update = String.Format("update Number set infors=N'{0}',image='{1}' where number='{2}'", content, imgURL, id);
                cmd = new SqlCommand(update, con);
                status = cmd.ExecuteNonQuery();
            }
            else
            {
                string insert = String.Format("Insert into Number values ('{0}',N'{1}','{2}')", id, content, imgURL);
                cmd = new SqlCommand(insert, con);
                status = cmd.ExecuteNonQuery();
            }
            con.Close();
            if (status == 1)
                return "OK";
            else
                return "NOT OK";
        }
        //############################ Upload to the BLOB #################################################
        
        public string BlobUpload(string number, string imgPath)
        {
            string connectionString = "DefaultEndpointsProtocol=https;AccountName=sqlvaqgtqjtinwypuq;AccountKey=RCVhoilPxLPIot4syztuMHlPbjN5xFDmPiKBkCUCh39T7r0l54IcH6QvmppmTAYNnXd6nAsFfaBLtamRQwBJaw==;EndpointSuffix=core.windows.net";

            string blobName = number+".png";

            // Get a reference to a container
            BlobContainerClient container = new BlobContainerClient(connectionString, "image");

            // Get a reference to a blob
            BlobClient blob = container.GetBlobClient(blobName);

            // Upload local file at a given path to Azure storage

            blob.Upload(imgPath);
            return "Ok";
        }

        //############################ Receive info form from calling api #################################
        [HttpPost]
        [Route("Upload")]
        public IActionResult Upload()
        {
            try
            {
                var file = Request.Form.Files[0];
                string a = file.GetType().ToString();
                var pathToSave = Path.Combine(this._env.ContentRootPath,"wwwroot\\image");

                if (file.Length > 0)
                {
                    var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
                    var savePath = Path.Combine(pathToSave,fileName + ".png");
                    var dbPath = "https://pythagoras.azurewebsites.net/image/" + fileName + ".png";
                    var content=ContentDispositionHeaderValue.Parse(file.ContentDisposition).Name.Trim('"');
                    using (var stream = new FileStream(savePath, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }
                    addNumber(fileName,content,dbPath);
                    return Ok(new { dbPath });
                }
                else
                {
                    return BadRequest();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex}");
            }
        }

        //########################################## Return result to client ################################################
        public string calcNumb(string input){
            int numb=input.ToCharArray().Sum(c => c - '0');
            if(numb>11 && numb!=22){
                return calcNumb(numb.ToString());
            }
            else if(numb==22){
                return "22";
            }
            else
                return numb.ToString();
        }
        
        //######################################################################################################
        
        [HttpGet]
        [Route("getResult")]
        public string getResult(string date){
            date=date.Replace("-",string.Empty);
            string number=calcNumb(date);
            SqlConnection con= new SqlConnection(DB);
            SqlConnection con2= new SqlConnection(DB);
            con.Open();           
            con2.Open(); 
            string cmdText="Select * from Number where number="+number;
            string cmdText2="Select count(*) from Number where number="+number;
            SqlCommand cmd=new SqlCommand(cmdText,con);
            SqlCommand cmd2=new SqlCommand(cmdText2,con2);
            DataTable dataTable=new DataTable();
            SqlDataReader dataReader= cmd.ExecuteReader();
            int exist=(int)cmd2.ExecuteScalar();
            if (exist > 0)
                dataTable.Load(dataReader);
            else{
                string errTime= DateTime.Now.ToString("F");
                string errText="There are some problem with number "+number+", please check";
                cmdText=String.Format("Insert into Log values ('{0}','{1}')", errTime ,errText);
                SqlConnection con3= new SqlConnection(DB);
                con3.Open();
                SqlCommand cmd3 = new SqlCommand(cmdText,con3);
                cmd3.ExecuteNonQuery();
                con.Close();
                con2.Close();
                con3.Close();
                return "Error";
            }
            con.Close();
            con2.Close();
            return JsonConvert.SerializeObject(dataTable);
        }

        //######################################################################################################

        [HttpPost]
        [Route("addFeedBack")]
        public string addFeedback(string email, string fb, string time){
            time=DateTime.Now.ToString("F");
            SqlConnection con = new SqlConnection(DB);
            con.Open();
            SqlCommand cmd=new SqlCommand();
            string insert = String.Format("Insert into FeedBack values (N'{0}','{1}','{2}')", fb.Replace("'",string.Empty).Replace("\"",string.Empty), email, time);
            cmd = new SqlCommand(insert, con);
            int status = cmd.ExecuteNonQuery();
            con.Close();
            if (status == 1)
                return JsonConvert.SerializeObject("OK");
            else
                return JsonConvert.SerializeObject("NOT OK");
        }

        //######################################################################################################

        [HttpGet]
        [Route("getSiteMap")]
        public string addFeedback(){
            SqlConnection con= new SqlConnection(DB);
            con.Open();            
            string cmdText="Select * from SiteMap";
            SqlCommand cmd=new SqlCommand(cmdText,con);
            SqlDataReader dataReader= cmd.ExecuteReader();
            DataTable dataTable=new DataTable();
            dataTable.Load(dataReader);
            con.Close();
            return JsonConvert.SerializeObject(dataTable);
        }

        //######################################################################################################

        [HttpGet]
        [Route("getIssue")]
        public string getIssue(){
            SqlConnection con= new SqlConnection(DB);
            con.Open();            
            string cmdText="Select * from Log";
            SqlCommand cmd=new SqlCommand(cmdText,con);
            SqlDataReader dataReader= cmd.ExecuteReader();
            DataTable dataTable=new DataTable();
            dataTable.Load(dataReader);
            con.Close();
            return JsonConvert.SerializeObject(dataTable);
        }

        //######################################################################################################
        [HttpGet]
        [Route("updateIssue")]
        public string updateIssue(string time){
            SqlConnection con= new SqlConnection(DB);
            con.Open();            
            string cmdText="Delete from Log where time like '"+time+"'";
            SqlCommand cmd=new SqlCommand(cmdText,con);
            int rows_affected =cmd.ExecuteNonQuery();
            con.Close();
            return rows_affected.ToString();
        }
        
    }
}
