using Assesment.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;
using System.Data;
using System.Data.SqlClient;

namespace Assesment.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StudentsController : Controller
    {
        private IConfiguration Configuration;

        private readonly string baseUrl;
        private readonly string studentEndpoint;

        //This help to access values from configuration files (appsettings.json)
        public StudentsController(IConfiguration configuration)
        {
            Configuration = configuration;

            //get API
            baseUrl = Configuration["ExternalAPI:BaseUrl"];
            studentEndpoint = Configuration["ExternalAPI:StudentEndpoint"];
        }


        SqlConnection connString;
        SqlCommand cmd;

        //Get Student details from API and save in DB
        [Route("FetchAndSaveStudents")]
        [HttpGet]
        public async Task<IActionResult> FetchAndSaveStudents()
        {
            try
            {
                // Create an HTTP client and set the base URL
                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(baseUrl);

                // Send a GET request to fetch student data from the external API
                var response = await client.GetAsync(studentEndpoint);

                // Failed to fetch
                if (!response.IsSuccessStatusCode)
                {
                    return BadRequest("Failed to fetch students from external API.");
                }

                // Read the response content as a JSON string
                var jsonData = await response.Content.ReadAsStringAsync();

                // Turn the JSON into a list of students 
                var studentList = JsonConvert.DeserializeObject<List<StudentModel>>(jsonData);

                // Givea message if API is null or no data
                if (studentList == null || studentList.Count == 0)
                {
                    return BadRequest("No student data to save.");
                }

                // Open SQL connection
                connString = new SqlConnection(this.Configuration.GetConnectionString("DefaultConnection"));
                connString.Open();

                // Create list of messages
                List<string> messages = new List<string>();

                // Add loop to check each student in studentList
                foreach (var student in studentList)
                {
                    // Check if a student with the same Id already exists in the database
                    cmd = new SqlCommand("SELECT COUNT(*) FROM StudentsList WHERE Id = @Id", connString);
                    cmd.Parameters.AddWithValue("@Id", student.Id);
                    int exists = (int)cmd.ExecuteScalar();

                    if (exists > 0)
                    {
                        // If exists, skip inserting and add a message
                        messages.Add($"Student with Id {student.Id} already exists. Skipping insert.");
                        continue;
                    }

                    // Save student data if not already present
                    cmd = new SqlCommand("INSERT INTO StudentsList(Id, StudentID, StudentName, StudentAge) VALUES(@Id, @StudentID, @StudentName, @StudentAge)", connString);
                    cmd.Parameters.AddWithValue("@Id", student.Id);
                    cmd.Parameters.AddWithValue("@StudentID", student.StudentID);
                    cmd.Parameters.AddWithValue("@StudentName", student.StudentName);
                    cmd.Parameters.AddWithValue("@StudentAge", student.StudentAge);
                    cmd.ExecuteNonQuery();

                    // Add success message
                    messages.Add($"Student with Id {student.Id} inserted successfully.");
                }

                // Close the SQL connection
                connString.Close();

                // Show all message
                return Ok(new
                {
                    message = "Student import process completed.",
                    details = messages
                });
            }
            catch (Exception ex)
            {
                // Return error message if exception occurs
                return BadRequest(new { error = ex.Message });
            }
        }

        //GEt all Student List
        [Route("GetStudentList")]
        [HttpGet]
        public JsonResult Index()
        {
            // Create an empty list that will store students we get from the database
            List<StudentModel> studlist = new List<StudentModel>();

            // SQL connection
            connString = new SqlConnection(this.Configuration.GetConnectionString("DefaultConnection"));
            DataTable dt = new DataTable();

            // Open the database connection
            connString.Open();

            // SQL command to select all records from StudentsList table
            cmd = new SqlCommand("SELECT * FROM StudentsList", connString);

            // Data adapter to fill DataTable with results of the SQL command
            SqlDataAdapter ad = new SqlDataAdapter(cmd);
            ad.Fill(dt);

            // Close the database connection
            connString.Close();

            // Get details from Datatable and save each
            foreach (DataRow row in dt.Rows)
            {
                StudentModel stud = new StudentModel
                {
                    Id = Convert.ToInt32(row["Id"]),
                    StudentID = row["StudentID"].ToString(),
                    StudentName = row["StudentName"].ToString(),
                    StudentAge = Convert.ToInt32(row["StudentAge"]),
                };

                // Add each student object to the list
                studlist.Add(stud);
            }

            // Return the list of students as JSON
            return Json(studlist);
        }


        //Find Student By ID
        [Route("FindById/{id}")]
        [HttpGet]
        public async Task<ActionResult> FindById(int id)
        {
            StudentModel student = null;

            // Find student by ID
            connString = new SqlConnection(this.Configuration.GetConnectionString("DefaultConnection"));
            cmd = new SqlCommand("SELECT * FROM StudentsList WHERE Id = @Id", connString);
            cmd.Parameters.AddWithValue("@Id", id);

            try
            {
                // Open the connection and reader
                connString.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                // Student Found in DB
                if (reader.Read())
                {
                    student = new StudentModel
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        StudentID = reader["StudentID"].ToString(),
                        StudentName = reader["StudentName"].ToString(),
                        StudentAge = Convert.ToInt32(reader["StudentAge"])
                    };
                }

                // Close the reader and the connection
                reader.Close();
                connString.Close();

                // success message
                if (student != null)
                    return Ok(new { message = "Found in DB", data = student });


                // If student not found in DB, fetch data from external API

                // Create HTTP client and set base URL
                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(baseUrl);

                // Send GET request to fetch students data from external API
                var response = await client.GetAsync(studentEndpoint);

                var jsonData = await response.Content.ReadAsStringAsync();
                var studentList = JsonConvert.DeserializeObject<List<StudentModel>>(jsonData);

                // Search for the requested student by Id in the API data
                student = studentList?.FirstOrDefault(s => s.Id == id);

                // Display message if student not found
                if (student == null)
                {
                    return NotFound("Student not found in DB or external API.");
                }

                // Save the student details
                connString = new SqlConnection(this.Configuration.GetConnectionString("DefaultConnection"));
                connString.Open();

                cmd = new SqlCommand("INSERT INTO StudentsList(Id, StudentID, StudentName, StudentAge) VALUES(@Id, @StudentID, @StudentName, @StudentAge)", connString);
                cmd.Parameters.AddWithValue("@Id", student.Id);
                cmd.Parameters.AddWithValue("@StudentID", student.StudentID);
                cmd.Parameters.AddWithValue("@StudentName", student.StudentName);
                cmd.Parameters.AddWithValue("@StudentAge", student.StudentAge);

                cmd.ExecuteNonQuery();

                connString.Close();

                // success messsage
                return Ok(new { message = "Not found in DB, retrieved from external API and saved.", data = student });
            }
            catch (Exception ex)
            {
                // error message 500
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

    }
}
