using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FuzzySharp;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace FuzzyMatch
{
    class Program
    {
        static void Main(string[] args)
        {

            Stopwatch watch = new Stopwatch();
            watch.Start();

            Console.WriteLine("Getting Connection ...");

            var datasource = @"hsc-sql-2016\BITEAM";
            var database = "TrakCareBI";

            //Connection string 
            string connString = @"Data Source=" + datasource + ";Initial Catalog="
                        + database + ";Persist Security Info=True;Trusted_Connection=True";

            //Create instanace of database connection
            SqlConnection conn = new SqlConnection(connString);
            conn.Open();

            var namesSQL = @"
SELECT *
FROM OPENQUERY(HSSDPRD, 
'SELECT 
         PAPMI_No as URN
       , PAPMI_Name2 as FirstName
       , PAPMI_Name as LastName
       , PAPMI_RowId->PAPER_Dob as DOB
       , PAPMI_PAPER_DR->PAPER_StName as Address1
       , PAPMI_PAPER_DR->PAPER_ForeignAddress as Address2
      -- , PAPMI_RowId->PAPER_Zip_DR->CTZIP_Code as PostCode
       , PAPMI_DVAnumber as SocialSecurityNumber
       , PAPMI_RowId->PAPER_Sex_DR->CTSEX_Desc as Gender
FROM PA_PatMas
WHERE PAPMI_Name2 NOT LIKE ''zz%''

AND PAPMI_Active is NULL
')";

            //Create DataTable to hold SQL query data and fill
            DataTable table = new DataTable();
            table.Columns.Add("URN", typeof(int));
            table.Columns.Add("FirstName", typeof(string));
            table.Columns.Add("LastName", typeof(string));
            table.Columns.Add("DOB", typeof(string));
            table.Columns.Add("Address1", typeof(string));
            //table.Columns.Add("Address2", typeof(string));
            //table.Columns.Add("PostCode", typeof(string));
            table.Columns.Add("SocialSecurityNumber", typeof(string));
            table.Columns.Add("Gender", typeof(string));

            SqlCommand cmd = new SqlCommand(namesSQL, conn);

            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
            {
                adapter.Fill(table);
            }
            conn.Close();


            //Console info
            watch.Stop();
            TimeSpan SqlTime = watch.Elapsed;
            Console.WriteLine($"SQL took {SqlTime.Minutes} minuites and {SqlTime.Seconds} seconds to return query");
            watch.Restart();
            Console.WriteLine("Working...\n");

            DataTable tableDistinct = table.DefaultView.ToTable( /*distinct*/ true);


            //Filter by gender and split data into 2 DataTables before adding to DataSet
            DataView dvF = tableDistinct.DefaultView;
            dvF.RowFilter = "Gender = 'Female'";
            DataTable femaleDT = dvF.ToTable();

            DataView dvM = tableDistinct.DefaultView;
            dvF.RowFilter = "Gender = 'Male'";
            DataTable maleDT = dvF.ToTable();

            DataSet GendersDS = new DataSet();
            GendersDS.Tables.Add(femaleDT);
            GendersDS.Tables.Add(maleDT);

            GendersDS.Tables[0].TableName = "Female";
            GendersDS.Tables[1].TableName = "Male";

            string[] letters =
            {
                "A",
                "B",
                "C",
                "D",
                "E",
                "F",
                "G",
                "H",
                "I",
                "J",
                "K",
                "L",
                "M",
                "N",
                "O",
                "P",
                "Q",
                "R",
                "S",
                "T",
                "U",
                "V",
                "W",
                "X",
                "Y",
                "Z"
            };

            var alphaDict = new List<string>(letters);

            List<string> demographics = new List<string>();

            string urn = string.Empty;
            string toFuzz = string.Empty;
            string dems = string.Empty;
            string ssn = string.Empty;

            Dictionary<int, List<string>> rowsListDict = new Dictionary<int, List<string>>();

            string notepad = @"M:\My Documents\Tests\FuzzyMatch\.txt";

            if (File.Exists(notepad))
            {
                File.Delete(notepad);
            }
            StreamWriter sw = new StreamWriter(@"M:\My Documents\Tests\FuzzyMatch\FuzzyResults.txt");

            var xlsxFile = $@"M:\My Documents\Tests\FuzzyMatch\FuzzyResults.xlsx";

            if (File.Exists(xlsxFile))
            {
                File.Delete(xlsxFile);
            }

            
            foreach (DataTable genderGroup in GendersDS.Tables)
            {
                var n = 0;
                foreach (DataRow row in genderGroup.Rows)
                {

                    urn = row["URN"].ToString();

                    toFuzz = toFuzz += string.Join(" ",

                        row["FirstName"].ToString(),
                        row["LastName"].ToString(),
                        row["DOB"].ToString().Replace("00:00:00", ""));

                    toFuzz = toFuzz.Trim();


                    dems = dems += string.Join(" ",
                        row["Address1"].ToString(),
                        row["Address2"].ToString());
                        //row["PostCode"].ToString(),

                    ssn = row["SocialSecurityNumber"].ToString();

                    rowsListDict.Add(n, new List<string>() { urn, toFuzz, dems, ssn });
                    
                    urn = string.Empty;
                    toFuzz = string.Empty;
                    dems = string.Empty;
                    ssn = string.Empty;
                    n = n + 1;

                }
                //foreach (var item in rowsListDict)
                //{
                //    Console.WriteLine($"{item.Key} : {item.Value[0]}");

                //}

                DataTable final = new DataTable();

                final.Columns.Add("URN1", typeof(int));
                final.Columns.Add("Name1", typeof(string));
                final.Columns.Add("URN2", typeof(int));
                final.Columns.Add("Name2", typeof(string));
                final.Columns.Add("NameRatio", typeof(int));
                final.Columns.Add("Address1", typeof(string));
                final.Columns.Add("Address2", typeof(string));
                final.Columns.Add("SSN1", typeof(string));
                final.Columns.Add("SSN2", typeof(string));

                foreach (var letter in letters)
            {
                for (int i = 0; i < rowsListDict.Count - 1; i++)
                {
                    for (int j = i + 1; j < rowsListDict.Count; j++)
                    {
                        var urn1 =  rowsListDict[i][0];
                        var name1 = rowsListDict[i][1];
                        var dems1 = rowsListDict[i][2];
                        var ssn1 = rowsListDict[i][3];

                        var urn2 = rowsListDict[j][0];
                        var name2 = rowsListDict[j][1];
                        var dems2 = rowsListDict[j][2];
                        var ssn2 = rowsListDict[i][3];

                            if (name1.StartsWith(letter.ToString()) && name2.StartsWith(letter))   
                        {
                            var ratio = Fuzz.Ratio(name1, name2);

                            if (ratio < 100 && ratio > 94)  
                                {
                                

                                final.Rows.Add(urn1, name1, urn2, name2, ratio, dems1, dems2, ssn1, ssn2);                           

                               
                            }
                        }
                    }
                }
            }



                DataColumn DR = final.Columns.Add("DemsRtio", typeof(int));
                DR.SetOrdinal(7);

                foreach (DataRow row in final.Rows)
                {
                    var dem1 = row["Address1"].ToString();
                    var dem2 = row["Address2"].ToString();

                    var ratio = Fuzz.Ratio(dem1, dem2);

                    row["DemsRtio"] = ratio;
                }

                DataColumn SR = final.Columns.Add("SSNRatio", typeof(int));
                SR.SetOrdinal(10);

                foreach (DataRow row in final.Rows)
                {
                    var ssn1 = row["SSN1"].ToString();
                    var ssn2 = row["SSN2"].ToString();

                    var ratio = Fuzz.Ratio(ssn1, ssn2);

                    row["SSNRatio"] = ratio;
                }


                foreach(DataRow row in final.Rows)
                {

                    Console.WriteLine(String.Join(";", row.ItemArray));
                    sw.WriteLine(String.Join(";", row.ItemArray));
                }


                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            FileInfo fileInfo = new FileInfo(xlsxFile);
            using (ExcelPackage package = new ExcelPackage(fileInfo))
            {

                ExcelWorksheet ws = package.Workbook.Worksheets.Add($"{genderGroup}");

                ws.Cells["A1"].LoadFromDataTable(final, true);
                ws.Cells.AutoFitColumns();
                ws.Cells.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                ws.View.FreezePanes(2, 1);
                package.Save();
                package.Dispose();
            }
            rowsListDict.Clear();
        }
        watch.Stop();
            TimeSpan C_SharpTime = watch.Elapsed;

            Console.WriteLine($"C# took {C_SharpTime.Minutes} minuites and {C_SharpTime.Seconds} seconds to process and write the data.");
            Console.WriteLine("Finished!");
            
            sw.WriteLine($"Finished @ {DateTime.Now}");
            sw.Close();
        }
    }
}
