﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Workforce.Models;
using Workforce.Models.ViewModels;
using System.Data.SqlClient;

namespace Workforce.Controllers
{
    public class InstructorController : Controller
    {
        private readonly IConfiguration _config;

        public InstructorController(IConfiguration config)
        {
            _config = config;
        }

        public IDbConnection Connection
        {
            get
            {
                return new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            }
        }

        public async Task<IActionResult> Index()
        {

            string sql = @"
            select
                i.Id,
                i.FirstName,
                i.LastName,
                i.SlackHandle,
                i.Specialty,
                c.Id,
            from Instructor i
            join Cohort c on i.CohortId = c.Id
        ";

            using (IDbConnection conn = Connection)
            {
                Dictionary<int, Instructor> instructors = new Dictionary<int, Instructor>();

                var instructorQuerySet = await conn.QueryAsync<Instructor, Cohort, Instructor>(
                        sql,
                        (instructor, cohort) => {
                            if (!instructors.ContainsKey(instructor.Id))
                            {
                                instructors[instructor.Id] = instructor;
                            }
                            instructors[instructor.Id].Cohort = cohort;
                            return instructor;
                        }
                    );
                return View(instructors.Values);

            }
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            string sql = $@"
            select
                i.Id,
                i.FirstName,
                i.LastName,
                i.SlackHandle,
                i.Specialty
            from Instructor i
            join Cohort c on i.CohortId = c.Id
            WHERE i.Id = {id}
            ";

            using (IDbConnection conn = Connection)
            {

                Instructor instructor = (await conn.QueryAsync<Instructor>(sql)).ToList().Single();

                if (instructor == null)
                {
                    return NotFound();
                }

                return View(instructor);
            }
        }

        private async Task<SelectList> CohortList(int? selected)
        {
            using (IDbConnection conn = Connection)
            {
                // Get all cohort data
                List<Cohort> cohorts = (await conn.QueryAsync<Cohort>("SELECT Id, Name FROM Cohort")).ToList();

                // Add a prompting cohort for dropdown
                cohorts.Insert(0, new Cohort() { Id = 0, Name = "Select cohort..." });

                // Generate SelectList from cohorts
                var selectList = new SelectList(cohorts, "Id", "Name", selected);
                return selectList;
            }
        }

        public async Task<IActionResult> Create()
        {
            using (IDbConnection conn = Connection)
            {
                ViewData["CohortId"] = await CohortList(null);
                return View();
            }
        }

        // POST: Instructor/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Instructor instructor)
        {

            if (ModelState.IsValid)
            {
                string sql = $@"
                    INSERT INTO Instructor
                        ( Id, FirstName, LastName, SlackHandle, Specialty, CohortId )
                        VALUES
                        ( null
                            , '{instructor.FirstName}'
                            , '{instructor.LastName}'
                            , '{instructor.SlackHandle}'
                            , '{instructor.Specialty}'
                            , {instructor.Cohort.Id}
                        )
                    ";

                using (IDbConnection conn = Connection)
                {
                    int rowsAffected = await conn.ExecuteAsync(sql);

                    if (rowsAffected > 0)
                    {
                        return RedirectToAction(nameof(Index));
                    }
                }
            }

            // ModelState was invalid, or saving the Instructor data failed. Show the form again.
            using (IDbConnection conn = Connection)
            {
                IEnumerable<Cohort> cohorts = (await conn.QueryAsync<Cohort>("SELECT Id, Name FROM Cohort")).ToList();
                // ViewData["CohortId"] = new SelectList (cohorts, "Id", "Name", instructor.CohortId);
                ViewData["CohortId"] = await CohortList(instructor.Cohort.Id);
                return View(instructor);
            }
        }
//GET for the Instructor
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            string sql = $@"
                SELECT
                    s.Id,
                    s.FirstName,
                    s.LastName,
                    s.SlackHandle,
                    s.CohortId,
                    c.Id,
                    c.Name
                FROM Student s
                JOIN Cohort c on s.CohortId = c.Id
                WHERE s.Id = {id}";

            using (IDbConnection conn = Connection)
            {
                StudentEditViewModel model = new StudentEditViewModel(_config);

                model.Student = (await conn.QueryAsync<Student, Cohort, Student>(
                    sql,
                    (student, cohort) => {
                        student.Cohort = cohort;
                        return student;
                    }
                )).Single();

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StudentEditViewModel model)
        {
            if (id != model.Student.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                string sql = $@"
                UPDATE Student
                SET FirstName = '{model.Student.FirstName}',
                    LastName = '{model.Student.LastName}',
                    SlackHandle = '{model.Student.SlackHandle}',
                    CohortId = {model.Student.CohortId}
                WHERE Id = {id}";

                using (IDbConnection conn = Connection)
                {
                    int rowsAffected = await conn.ExecuteAsync(sql);
                    if (rowsAffected > 0)
                    {
                        return RedirectToAction(nameof(Index));
                    }
                    throw new Exception("No rows affected");
                }
            }
            else
            {
                return new StatusCodeResult(StatusCodes.Status406NotAcceptable);
            }
        }

        public async Task<IActionResult> DeleteConfirm(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            string sql = $@"
                select
                    s.Id,
                    s.FirstName,
                    s.LastName,
                    s.SlackHandle
                from Student s
                WHERE s.Id = {id}";

            using (IDbConnection conn = Connection)
            {

                Student student = (await conn.QueryAsync<Student>(sql)).ToList().Single();

                if (student == null)
                {
                    return NotFound();
                }

                return View(student);
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {

            string sql = $@"DELETE FROM Student WHERE Id = {id}";

            using (IDbConnection conn = Connection)
            {
                int rowsAffected = await conn.ExecuteAsync(sql);
                if (rowsAffected > 0)
                {
                    return RedirectToAction(nameof(Index));
                }
                throw new Exception("No rows affected");
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
