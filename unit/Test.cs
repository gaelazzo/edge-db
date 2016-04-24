using System;
using System.Configuration;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;


namespace unit {
    [SetUpFixture]
    public class setupDb {
        public static int open() {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param["connectionString"] = getConnectionString();
            param["driver"] = getDriver();
            param["cmd"] = "open";
            EdgeCompiler ec = new EdgeCompiler();
            var t = ec.CompileFunc(param);
            var res = t.Invoke(null);
            Task.WaitAll(res);
            return (int)res.Result;
        }

        public static void close(int handler) {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param["cmd"] = "close";
            param["handler"] = handler;
            param["driver"] = getDriver();
            EdgeCompiler ec = new EdgeCompiler();
            var t = ec.CompileFunc(param);
            Task<object> resClose = t.Invoke(null);
            Task.WaitAll(resClose);
        }

        public static string getConnectionString() {
            string connName = "mySql";
            if (System.Environment.GetEnvironmentVariable("TRAVIS") != null) {
                connName = "travis";
            }
            return ConfigurationManager.ConnectionStrings[connName].ConnectionString;
        }

        public static string getDriver() {
            string connName = "mySql";
            if (System.Environment.GetEnvironmentVariable("TRAVIS") != null) {
                connName = "travis";
            }
            return ConfigurationManager.AppSettings["driver." + connName];
        }

        public static void runScript(int handler, string script) {
            Dictionary<string, object> param = new Dictionary<string, object> {
                ["source"] = script,
                ["cmd"] = "nonquery",
                ["handler"] = handler,
                ["driver"] = getDriver()
            };
            EdgeCompiler ec = new EdgeCompiler();
            var t = ec.CompileFunc(param);
            var res = t.Invoke(null);
            Task.WaitAll(res);
        }


        public static void runFile(int handler, string scriptName) {
            string path = Path.Combine(
                  Directory.GetParent(
                      Directory.GetParent(
                          Directory.GetCurrentDirectory()
                      ).FullName
                  ).FullName,
                  scriptName);
            string[] all = File.ReadAllLines(path);
            string script = "";
            int i = 0;
            while (i < all.Length) {
                string s = all[i];
                if (s.TrimEnd().ToUpper() == "GO") {
                    if (script != "") {
                        runScript(handler, script);
                        script = "";
                    }
                }
                else {
                    script += s + "\n\r";
                }
                i = i + 1;
            }
            if (script != "") {
                runScript(handler, script);
            }
        }


        [SetUp]
        public void runBeforeAnyTests() {
            int handler = open();
            runFile(handler, "setup.sql");
            close(handler);
        }

        [TearDown]
        public void runAfterAnyTests() {
            int handler = open();
            runFile(handler, "destroy.sql");
            close(handler);
        }
    }

    [TestFixture()]
	public class UnitTest1 {

        [Test ()]
		public void compilerExists() {

			EdgeCompiler ec = new EdgeCompiler();
			Assert.IsTrue(ec.GetType().GetMethod("CompileFunc") != null, "EdgeCompiler has CompileFunc");

		}


		[Test ()]
		public void openConnection() {
			Dictionary<string, object> param = new Dictionary<string, object>();
			param["connectionString"] = setupDb.getConnectionString();
			param["driver"] = setupDb.getDriver();
			param["cmd"] = "open";
			EdgeCompiler ec = new EdgeCompiler();
			var t = ec.CompileFunc(param);
			var res = t.Invoke(null);
			Task.WaitAll(res);
			Assert.AreEqual(res.Status, TaskStatus.RanToCompletion, "Open executed");
			Assert.IsFalse(res.IsFaulted);
			Assert.IsInstanceOf(typeof (int), res.Result,"Open returned an int");           
		}

		[Test ()]
		public void openBadConnection() {
			Dictionary<string, object> param = new Dictionary<string, object>();
			param["connectionString"] = "bad connection";
			param["driver"] = setupDb.getDriver();
			param["cmd"] = "open";
			param["timeout"] = 3;
			EdgeCompiler ec = new EdgeCompiler();
			var t = ec.CompileFunc(param);
			Task <object> res=null;
			try {
				res = t.Invoke(null);
				Task.WaitAll(res);
				Assert.IsFalse(res.IsFaulted,"Open bad connection should throw");
			}
			catch {
				Assert.IsNotNull(res, "Open task should exist");
				Assert.AreEqual(TaskStatus.Faulted, res.Status, "Open bad connection should throw");                
			}
		}

		[Test ()]
		public void closeConnection() {
			int handler = setupDb.open();

			Dictionary<string, object>  param = new Dictionary<string, object>();
			param["cmd"] = "close";
			param["handler"] = handler;
			param["driver"] = setupDb.getDriver();
			EdgeCompiler ec = new EdgeCompiler();
			var t = ec.CompileFunc(param);            
			try {
				var resClose = t.Invoke(null);
				Task.WaitAll(resClose);
				Assert.IsFalse(resClose.IsFaulted, "Close connection should success");
			}
			catch {
				Assert.AreEqual(true,false, "Close connection should not throw");
			}
		    
		}

		[Test ()]
		public void setupScriptShouldExist() {

			string path = Path.Combine(
				Directory.GetParent(
					Directory.GetParent(
						Directory.GetCurrentDirectory()
					).FullName
				).FullName,
				"setup.sql");
			Assert.IsTrue(File.Exists(path), "setup script should be present in "+path);
		}

		[Test ()]
		public void getDbDate() {
			int handler = setupDb.open();

			Dictionary<string, object> param = new Dictionary<string, object> {
                ["source"] = "select now()",
				["cmd"] = "nonquery",
				["handler"] = handler,
				["driver"] = setupDb.getDriver()
			};
			EdgeCompiler ec = new EdgeCompiler();
			var t = ec.CompileFunc(param);
		    var res = t.Invoke(null);
		    Task.WaitAll(res);
		    Assert.IsInstanceOf(typeof(Dictionary<string,object>), res.Result, "non query should return  a dictionary ");
            Assert.IsTrue(((Dictionary<string, object>) res.Result).ContainsKey("rowcount"), "non query should return  a dictionary with rowcount");

            setupDb.close(handler);
		}

	  

        [Test()]
        public void setupScriptShouldBeRunned() {
            int handler = setupDb.open();
            string path = Path.Combine(
                Directory.GetParent(
                    Directory.GetParent(
                        Directory.GetCurrentDirectory()
                    ).FullName
                ).FullName,
                "setup.sql");
            setupDb.runFile(handler,"setup.sql");
            setupDb.close(handler);
            Assert.IsTrue(true, "setup Script does not throw when runned");
        }

        [Test()]
        public void updateSellerKind() {
            int handler = setupDb.open();

            Dictionary<string, object> param = new Dictionary<string, object> {
                ["source"] = "update sellerkind set rnd=rnd+1;",
                ["cmd"] = "nonquery",
                ["handler"] = handler,
                ["driver"] = setupDb.getDriver()
            };
            EdgeCompiler ec = new EdgeCompiler();
            var t = ec.CompileFunc(param);
            var tRes = t.Invoke(null);
            Task.WaitAll(tRes);
            Dictionary<string, object> res = (Dictionary<string, object>)tRes.Result;
            Assert.AreEqual(20,res["rowcount"]);

            setupDb.close(handler);
        }

        [Test()]
        public void selectFromCustomerKindShouldReturnDictionary() {
            int handler = setupDb.open();

            Dictionary<string, object> param = new Dictionary<string, object> {
                ["source"] = "select * from customerkind;",
                //["cmd"] = "nonquery",
                ["handler"] = handler,
                ["driver"] = setupDb.getDriver()
            };
            EdgeCompiler ec = new EdgeCompiler();
            var t = ec.CompileFunc(param);
            var tRes = t.Invoke(null);
            Task.WaitAll(tRes);            
            Assert.IsInstanceOf(typeof(List<object>), tRes.Result, "query without callback should return  a List<object> ");
            List<object> res = (List<object>) tRes.Result;
            Assert.AreEqual(1, res.Count, "select * from  CustomerKind should return 1 result set");
            Assert.IsInstanceOf(typeof(Dictionary<string,object>), res[0], "Result set is a dictionary<string,object>");
            Dictionary<string, object> resultSet = (Dictionary<string, object>) res[0];
            Assert.IsInstanceOf(typeof(Object[]), resultSet["meta"], "ResultSet.meta is a Object[] ");
            Assert.IsInstanceOf(typeof(List<object>), resultSet["rows"], "ResultSet.rows is a list<object> ");

            setupDb.close(handler);
        }

        [Test()]
        public void selectFromCustomerKindMetaShouldBeArrayOfColumnNames() {
            int handler = setupDb.open();

            Dictionary<string, object> param = new Dictionary<string, object> {
                ["source"] = "select * from customerkind;",
                //["cmd"] = "nonquery",
                ["handler"] = handler,
                ["driver"] = setupDb.getDriver()
            };
            EdgeCompiler ec = new EdgeCompiler();
            var t = ec.CompileFunc(param);
            var tRes = t.Invoke(null);
            Task.WaitAll(tRes);
            List<object> res = (List<object>)tRes.Result;
            Dictionary<string, object> resultSet = (Dictionary<string, object>)res[0];
            Object[] meta = (Object[])resultSet["meta"];
            Assert.AreEqual(meta[0], "idcustomerkind");
            Assert.AreEqual(meta[1], "name");
            Assert.AreEqual(meta[2], "rnd");
            Assert.AreEqual(meta.Length, 3);

            setupDb.close(handler);
        }

        [Test()]
        public void selectFromCustomerKindRowsShouldBeListOfObjects() {
            int handler = setupDb.open();

            Dictionary<string, object> param = new Dictionary<string, object> {
                ["source"] = "select * from customerkind;",
                //["cmd"] = "nonquery",
                ["handler"] = handler,
                ["driver"] = setupDb.getDriver()
            };
            EdgeCompiler ec = new EdgeCompiler();
            var t = ec.CompileFunc(param);
            var tRes = t.Invoke(null);
            Task.WaitAll(tRes);
            Assert.IsInstanceOf(typeof(List<object>), tRes.Result, "query without callback should return  a List<object[]> ");
            List<object> res = (List<object>)tRes.Result;
            Dictionary<string, object> resultSet = (Dictionary<string, object>)res[0];
            List <object> rows = (List<object>)resultSet["rows"];
            Assert.AreEqual(rows.Count, 40);
            for (int i = 0; i < 40; i++) {
                Object[] values = (Object[]) rows[i];
                Assert.AreEqual(values[0],  i*3);
                Assert.AreEqual(values[1].ToString(), "name" + (i*3));
                Assert.IsInstanceOf(typeof(Int32),values[2]);
            }
            setupDb.close(handler);
        }
    }
}

