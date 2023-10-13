﻿//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using ApiModels.Shared;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Http.HttpResults;
//using Microsoft.AspNetCore.Mvc;
//using TeamServer.Controllers;
//using TeamServer.Models;
//using TeamServer.Models.Dbstorage;
//using TeamServer.Models.Extras;
//using TeamServer.Models.TaskResultTypes;
//using TeamServer.Utilities;
////using DynamicEngLoading;

//namespace TeamServer.Services.Handle_Implants;

//public class Handle_Engineer : ControllerBase
//{
//        private readonly IEngineerService _engineers;
//        public static Socks4Proxy Proxy { get; set; }
//        public static Dictionary<string, EngineerTask> CommandIds = new Dictionary<string, EngineerTask>();
//        public static Dictionary<string, List<string>> EngineerChildIds = new Dictionary<string, List<string>>(); //keys are Engineers that have child engineers they can task, value is the childrens ids  
//        public static Dictionary<string, List<string>> PathStorage = new Dictionary<string, List<string>>(); // key is the Engineer Id, Value is a list of parent ids and ends with its own id, making its path. The path is a list element 0 is the http, and each new eleemnt is a layer deepr
//        public static Dictionary<string,int> EngineerCheckinCount = new Dictionary<string, int>(); //key is the engineer id, value is the number of times they have checked in
//        public static IEnumerable<EngineerTaskResult> results { get; set;}
       

//        public Handle_Engineer(IEngineerService engineers) //uses dependdency Injection to link  to service and crerate object instance. 
//        {
//            _engineers = engineers;
//        }
        
        
//        public async Task<IActionResult> HandleEngineerAsync(EngineerMetadata engineermetadata, HttpContext httpContext)                // the http tags are not always needed with IActionResults 
//        {
//            try
//            {
//                Httpmanager EngManager = (Httpmanager)managerService._managers.FirstOrDefault(m => m.Name == engineermetadata.ManagerName);
//                List<string> ResponseHeaders = new List<string>();
//                if (EngManager != null)
//                {
//                    ResponseHeaders = EngManager.c2Profile.ResponseHeaders.Split(',').ToList();
//                }
//                foreach (string header in ResponseHeaders)
//                {
//                    //split the header string at the first index of VALUE so the name is everything to the left of the VALUE and the value is everything to the right of the VALUE
//                    var headerName = header.Substring(0, header.IndexOf("VALUE"));
//                    var headerValue = header.Substring(header.IndexOf("VALUE") + 5);

//                    httpContext.Response.Headers.Add($"{headerName}", $"{headerValue}");
//                }

//                //only works on HTTP/HTTPS because the metadata for these calls comes from the headers.
//                var engineer = _engineers.GetEngineer(engineermetadata.Id);
//                if (engineer is null)                              // if Engineer is null then this is the first time connecting so send metadata and add to list
//                {
//                    engineer = new Engineer(engineermetadata)                // makes object of Engineer type, and passes in the incoming metadata for the first time 
//                    {
//                        ExternalAddress = httpContext.Connection.RemoteIpAddress.ToString(),
//                        ConnectionType = httpContext.Request.Scheme,
//                    };
//                    //check in here so it goes into the database with a checkin time
//                    _engineers.AddEngineer(engineer);                    // uses service too add Engineer to list
//                    EngineersController.engineerList.Add(engineer);
//                    if (DatabaseService.AsyncConnection == null)
//                    {
//                        DatabaseService.ConnectDb();
//                    }
//                    DatabaseService.AsyncConnection.InsertAsync((ExtImplant_DAO)engineer);
//                    PathStorage.Add(engineer.engineerMetadata.Id, new List<string> { engineer.engineerMetadata.Id });
//                    HardHatHub.AlertEventHistory(new HistoryEvent { Event = $"engineer {engineer.engineerMetadata.Id} checked in for the first time", Status = "Success" });
//                    LoggingService.EventLogger.ForContext("engineer Metadata",engineer.engineerMetadata,true).ForContext("connection Type", engineer.ConnectionType).Information($"engineer {engineer.engineerMetadata.ProcessId}@{engineer.engineerMetadata.Address} checked in for the first time");
                    
//                    //create the unique encryption key for this implant
//                    Encryption.GenerateUniqueKeys(engineer.engineerMetadata.Id);
//                    EngineerTask updateTaskKey = new EngineerTask
//                    {
//                        Command = "UpdateTaskKey",
//                        Id = Guid.NewGuid().ToString(),
//                        Arguments = new Dictionary<string, string> { { "TaskKey", Encryption.UniqueTaskEncryptionKey[engineer.engineerMetadata.Id] } },
//                        File = null,
//                        IsBlocking = false
//                    };
//                    engineer.QueueTask(updateTaskKey);
//                    //activate webhooks
//                    await HardHatHub.InvokeNewCheckInWebhook(engineer);

//                }
//                    //checkin and get/post data to or from the engineer 
//                    //checkin updates some of the properties for the engineer
//                    engineer.CheckIn();
//                    //update sleep 0 implants every 5 seconds to keep the database up to date without it being a huge performance hit
//                    if (engineer.engineerMetadata.Sleep == 0 && engineer.LastSeen < DateTime.UtcNow.AddSeconds(-5))
//                    {
//                        DatabaseService.AsyncConnection.UpdateAsync((ExtImplant_DAO)engineer);
//                    }
//                    else
//                    {
//                        DatabaseService.AsyncConnection.UpdateAsync((ExtImplant_DAO)engineer);
//                    }

//                    if (EngineerCheckinCount.ContainsKey(engineer.engineerMetadata.Id))
//                {
//                    EngineerCheckinCount[engineer.engineerMetadata.Id] += 1;
//                }
//                else
//                {
//                    EngineerCheckinCount.Add(engineer.engineerMetadata.Id, 1);
//                }

//                try
//                {
                    
//                    if (httpContext.Request.Method == "GET")
//                    {
//                        //this will happen every sleep cycle it triggers the hub to let the client know a engineer has checked back in  
//                        if (HardHatHub._clients.Count() >0)
//                        {
//                            await HardHatHub.CheckIn(engineer);
//                        }
//                        else if(HardHatHub._clients.Count() ==0 && engineer.engineerMetadata.Sleep > 0)
//                        {
//                            Console.WriteLine("No clients connected to the hub");
//                        }
//                    }
                    
//                    else if (httpContext.Request.Method == "POST") // engineer checking in and sending us the results of a command in a post call
//                    {
//                        byte[] encryptedData;
//                        using var ms = new MemoryStream();
//                        await httpContext.Request.Body.CopyToAsync(ms);
//                        encryptedData = ms.ToArray();
//                        //encryptedData has a length and a implant id at the beginning of the array so we need to remove those and save them in variables 
//                        //take the first 4 bytes of the array and convert them to an int to get the length of the encrypted data
//                        int implantIdLength = BitConverter.ToInt32(encryptedData.Take(4).ToArray(), 0);
//                        //tale the length skip the first 4 bytes and get the implant id from the array
//                        string implantId = Encoding.UTF8.GetString(encryptedData.Skip(4).Take(implantIdLength).ToArray());
//                        //set encryptedData to the rest of the array after the implant id
//                        encryptedData = encryptedData.Skip(4 + implantIdLength).ToArray();
                        
//                        // convert the EncryptedJson object to a byte array and decrypt it using the AES_Decrypt function
//                        byte[] decryptedBytes;
//                        if (Encryption.UniqueTaskEncryptionKey.ContainsKey(implantId))
//                        {
//                            //Console.WriteLine($"Uaing unique key {Encryption.UniqueTaskEncryptionKey[engineer.engineerMetadata.Id]}");
//                            decryptedBytes = Encryption.Engineer_AES_Decrypt(encryptedData,Encryption.UniqueTaskEncryptionKey[implantId]);
//                        }
//                        else
//                        {
//                            //Console.WriteLine($"using universal key {Encryption.UniversalTaskEncryptionKey}");
//                            decryptedBytes = Encryption.Engineer_AES_Decrypt(encryptedData,Encryption.UniversalTaskEncryptionKey);
//                        }
//                        //if decryptedBytes is null then the decryption failed & we should try using the pathStorage dictionary to find the correct key
//                        if (decryptedBytes == null)
//                        {
//                            Console.WriteLine("Bytes are null");
//                            if (PathStorage.ContainsKey(engineer.engineerMetadata.Id))
//                            {
//                                foreach (string key in PathStorage[engineer.engineerMetadata.Id])
//                                {
//                                    if (Encryption.UniqueTaskEncryptionKey.ContainsKey(key))
//                                    {
//                                        decryptedBytes = Encryption.Engineer_AES_Decrypt(encryptedData, Encryption.UniqueTaskEncryptionKey[key]);
//                                        if (decryptedBytes != null)
//                                        {
//                                            break;
//                                        }
//                                    }
//                                    decryptedBytes = Encryption.Engineer_AES_Decrypt(encryptedData, Encryption.UniversalTaskEncryptionKey);
//                                }
//                            }
//                        }

//                        results = decryptedBytes.Deserialize<IEnumerable<EngineerTaskResult>>(); //should hold the results of the Engineer response to a command

//                        // we build a list of engineer ids because some tasks can be from P2P implants and should go to tright place later
//                        List<string> engIds = new List<string>();
//                        foreach (var result in results)
//                        {
//                            // if the result type is a data chunk we need to add it to the data chunk dictionary and not process anything until we have all the chunks
//                            if (result.ResponseType == TaskResponseType.DataChunk)
//                            {
//                                DataChunk dataChunk = result.Result.Deserialize<DataChunk>();
//                                //get the engineer this task belongs to 
//                                Engineer taskedEngineer = _engineers.GetEngineer(result.EngineerId);
//                                if (dataChunk.Type == 1)
//                                {
//                                    if (taskedEngineer.TaskResultDataChunks.ContainsKey(result.Id))
//                                    {
//                                        taskedEngineer.TaskResultDataChunks[result.Id] = taskedEngineer.TaskResultDataChunks[result.Id].Concat(dataChunk.Data).ToArray();
//                                    }
//                                    else
//                                    {
//                                        taskedEngineer.TaskResultDataChunks.Add(result.Id, dataChunk.Data);
//                                    }
//                                    await HardHatHub.CheckIn(taskedEngineer);
//                                    continue;
//                                }
//                                //this means we have all the chunks and can process the data
//                                else if (dataChunk.Type == 2)
//                                {
//                                    result.Result = taskedEngineer.TaskResultDataChunks[result.Id];
//                                    result.ResponseType = (TaskResponseType)dataChunk.RealResponseType;
//                                    taskedEngineer.TaskResultDataChunks.Remove(result.Id);
//                                }
//                            }
//                            if (!engIds.Contains(result.EngineerId))
//                            {
//                                engIds.Add(result.EngineerId);
//                            }
//                            if (result.Status == EngTaskStatus.Running || result.Status == EngTaskStatus.Complete)
//                            {

//                                if (result.Command.Equals("SocksConnect", StringComparison.CurrentCultureIgnoreCase) || result.Command.Equals("socksReceive", StringComparison.CurrentCultureIgnoreCase))
//                                {
//                                    await Task.Run(async () => await Engineer_TaskPostProcess.PostProcess_SocksTask(result));
//                                }

//                                else if (CommandIds.ContainsKey(result.Id))
//                                {
//                                    if (CommandIds[result.Id].Command == "download")
//                                    {
//                                        string hostname = _engineers.GetEngineer(result.EngineerId).engineerMetadata.Hostname;
//                                        await Engineer_TaskPostProcess.PostProcess_DownloadTask(result, hostname);
//                                    }
//                                }

//                                else if (result.Command.Equals("rportsend", StringComparison.CurrentCultureIgnoreCase))
//                                {
//                                    await Engineer_TaskPostProcess.PostPorcess_RPortForward(result);
//                                }

//                                //performs the first checkin for P2P implants to get the pathing info, metadata, and add the new engineer to the needed lists. 
//                                else if (result.Command.Equals("P2PFirstTimeCheckIn", StringComparison.CurrentCultureIgnoreCase))
//                                {
//                                    string p2pEngMetadataString = await Engineer_TaskPostProcess.PostProcess_P2PFirstCheckIn(result, engineer);
//                                    byte[] p2pMetaDataByte = Convert.FromBase64String(p2pEngMetadataString);
//                                    EngineerMetadata p2pEngMetadata = p2pMetaDataByte.Deserialize<EngineerMetadata>();
//                                    var p2pengineer = _engineers.GetEngineer(p2pEngMetadata.Id);
//                                    if (p2pengineer is null)                              // if Engineer is null then this is the first time connecting so send metadata and add to list
//                                    {
//                                        // use the parent id to get the parents pid@address 
//                                        var parentEngineer = _engineers.GetEngineer(PathStorage[p2pEngMetadata.Id][0]);
//                                        var extralAddressP2PString = parentEngineer.engineerMetadata.ProcessId + "@" + parentEngineer.engineerMetadata.Address;

//                                        p2pengineer = new Engineer(p2pEngMetadata)                // makes object of Engineer type, and passes in the incoming metadata for the first time 
//                                        {
//                                            ExternalAddress = extralAddressP2PString,
//                                            ConnectionType = managerService._managers.FirstOrDefault(m => m.Name == p2pEngMetadata.ManagerName).Type.ToString(),
//                                        };
//                                        if (DatabaseService.AsyncConnection == null)
//                                        {
//                                            DatabaseService.ConnectDb();
//                                        }
//                                        DatabaseService.AsyncConnection.InsertAsync((ExtImplant_DAO)p2pengineer);
//                                        _engineers.AddEngineer(p2pengineer);                    // uses service too add Engineer to list
//                                        HardHatHub.AlertEventHistory(new HistoryEvent { Event = $"engineer {p2pengineer.engineerMetadata.Id} checked in for the first time", Status = "Success" });
//                                        LoggingService.EventLogger.ForContext("engineer Metadata", p2pengineer.engineerMetadata, true).ForContext("connection Type", p2pengineer.ConnectionType).Information($"engineer {p2pengineer.engineerMetadata.ProcessId}@{p2pengineer.engineerMetadata.Address} checked in for the first time");

//                                        //create the unique encryption key for this implant
//                                        Encryption.GenerateUniqueKeys(p2pengineer.engineerMetadata.Id);
//                                        EngineerTask updateTaskKey = new EngineerTask
//                                        {
//                                            Command = "UpdateTaskKey",
//                                            Id = Guid.NewGuid().ToString(),
//                                            Arguments = new Dictionary<string, string> { { "TaskKey", Encryption.UniqueTaskEncryptionKey[p2pengineer.engineerMetadata.Id] } },
//                                            File = null,
//                                            IsBlocking = false
//                                        };
//                                        p2pengineer.QueueTask(updateTaskKey);
//                                    }
//                                    //checkin and get/post data to or from the engineer 
//                                    p2pengineer.CheckIn();
//                                    if (EngineerCheckinCount.ContainsKey(p2pengineer.engineerMetadata.Id))
//                                    {
//                                        EngineerCheckinCount[p2pengineer.engineerMetadata.Id] += 1;
//                                    }
//                                    else
//                                    {
//                                        EngineerCheckinCount.Add(p2pengineer.engineerMetadata.Id, 1);
//                                    }

//                                }
//                                //allows for other engineers besides http to have a "check-in"
//                                else if (result.Command.Equals("CheckIn", StringComparison.CurrentCultureIgnoreCase))
//                                {
//                                    var p2pengineer = _engineers.GetEngineer(result.EngineerId);
//                                    //checkin and get/post data to or from the engineer 
//                                    p2pengineer.CheckIn();
//                                    if (EngineerCheckinCount.ContainsKey(p2pengineer.engineerMetadata.Id))
//                                    {
//                                        EngineerCheckinCount[p2pengineer.engineerMetadata.Id] += 1;
//                                    }
//                                    else
//                                    {
//                                        EngineerCheckinCount.Add(p2pengineer.engineerMetadata.Id, 1);
//                                    }
//                                }
//                                else if (result.Command.Equals("upload", StringComparison.CurrentCultureIgnoreCase))
//                                {
//                                    Task.Run(async () => await Engineer_TaskPostProcess.PostProcess_IOCFileUpload(result));
//                                }
//                            }
//                            if (result.IsHidden)
//                            {
//                                results = results.Where(x => x.Id != result.Id);
//                            }
//                            if (DatabaseService.AsyncConnection == null)
//                            {
//                                DatabaseService.ConnectDb();
//                            }
//                            if (result.IsHidden == false)
//                            {
//                                Task.Run( async() => await DatabaseService.AsyncConnection.InsertAsync((ExtImplantTaskResult_DAO)result));
//                                HardHatHub.AlertEventHistory(new HistoryEvent() { Event = $"Got response for task {result.Id}", Status = "Success" });
//                                string ResultValue = result.Result.Deserialize<MessageData>()?.Message ?? string.Empty;
//                                LoggingService.TaskLogger.ForContext("Task", result, true).ForContext("Task Result",ResultValue).Information($"Got response for task {result.Id}");
//                            }
//                        }
//                        foreach (string engId in engIds)
//                        {
//                            Engineer TaskedEng = _engineers.GetEngineer(engId);
//                            TaskedEng.AddTaskResults(results.Where(x => x.EngineerId == engId));
//                            // make another hub connection so I can invoke the GetTaskResults method on the client side
//                            if (HardHatHub._clients.Count > 0)
//                            {
//                                await HardHatHub.CheckIn(TaskedEng);
//                                //get a list of the taskIds for this engineer
//                                //call GetTaskIds with the ids of tasks from results 
//                                List<string> taskIds = new List<string>();
//                                foreach (EngineerTaskResult result in results)
//                                {
//                                    if (result.EngineerId == engId)
//                                    {
//                                        taskIds.Add(result.Id);
//                                    }
//                                }
//                                HardHatHub.ShowEngineerTaskResponse(TaskedEng.engineerMetadata.Id, taskIds);
//                            }
//                            else
//                            {
//                                Console.WriteLine("No clients connected");
//                            }
//                        }
//                    }
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine("error during check in");
//                    Console.WriteLine(ex.Message);
//                    Console.WriteLine(ex.StackTrace);
                        
//                }

//                try
//                {
//                    List<C2TaskMessage> c2TaskMessageArray = new List<C2TaskMessage>();  //will hold a group of C2TaskMessages, which each hold an encrypted byte array of tasks, byte array for the path the path is not encrypted beacuase anyone cna decrypte the first layer of data back int oa C2TaskMessage holding Encrypted Task, plain text path.
//                    //IEnumerable<EngineerTask> tasks = null;
//                    //end of checkin and task response posting, get pending tasks and respond to the engineer

//                    List<Engineer> TaskingEngs = new List<Engineer>();
//                    foreach(Engineer eng in _engineers.GetEngineers().Where(x => x._pendingTasks.Count() > 0))
//                    {
//                        if (!PathStorage.ContainsKey(eng.engineerMetadata.Id))
//                        {
//                            PathStorage.Add(eng.engineerMetadata.Id, new List<string>() { eng.engineerMetadata.Id });
//                        }
//                        // checking if the current Http implant is in its path, if it is then we know eng is a child of this http implant in some way and can be tasked by it.
//                        if (PathStorage[eng.engineerMetadata.Id].Contains(engineer.engineerMetadata.Id))
//                        {
//                            var engTasks = eng.GetPendingTasks();
//                            if (engTasks.Count() > 0)
//                            {
//                                TaskingEngs.Add(eng);
//                                foreach (var task in engTasks)
//                                {
//                                    //add the taskId to the Command dictionary so we can match the task id to the command id when we get the response from the engineer
//                                    if (!CommandIds.ContainsKey(task.Id))
//                                    {
//                                        CommandIds.Add(task.Id, task);
//                                    }
//                                    if (Engineer_TaskPreProcess.CommandsThatNeedPreProc.Contains(task.Command, StringComparer.OrdinalIgnoreCase))
//                                    {
//                                        await Engineer_TaskPreProcess.PreProcessTask(task, eng);
//                                    }
//                                    HardHatHub.AddTaskIdToPickedUpList(task.Id);
//                                }
//                                var taskArray = engTasks.Serialize();
//                                byte[] encryptedTaskArray;
//                                if (EngineerCheckinCount[eng.engineerMetadata.Id] > 1)
//                                {
//                                    //Console.WriteLine($"Using unique encryption key {Encryption.UniqueTaskEncryptionKey[eng.engineerMetadata.Id]}");
//                                    encryptedTaskArray = Encryption.Engineer_AES_Encrypt(taskArray,Encryption.UniqueTaskEncryptionKey[eng.engineerMetadata.Id]);
//                                }
//                                else
//                                {
//                                    //Console.WriteLine($"Using encryption key {Encryption.UniversalTaskEncryptionKey}");
//                                    encryptedTaskArray = Encryption.Engineer_AES_Encrypt(taskArray,Encryption.UniversalTaskEncryptionKey);
//                                }

//                                var c2TaskMessage = new C2TaskMessage { TaskData = encryptedTaskArray, PathMessage = PathStorage[eng.engineerMetadata.Id] };
//                                //do a console.WriteLine where the message each element in c2TaskMessage.PathMessage
//                                //Console.WriteLine($"path is {String.Join("->",c2TaskMessage.PathMessage)}");
//                                c2TaskMessageArray.Add(c2TaskMessage);
//                            }
                            
//                        }
//                    }


//                    if(TaskingEngs.Count() > 0)
//                    {
//                        var c2TaskMessageArraySeralized = c2TaskMessageArray.Serialize();
//                        var encrypedc2messageArray = Encryption.Engineer_AES_Encrypt(c2TaskMessageArraySeralized, Encryption.UniversialMessagePathKey);
//                        return File(encrypedc2messageArray, "application/octet-stream"); // This is what gets send back on the eng check in and if its not null we sent a task object.
//                    }
//                    else if (TaskingEngs.Count() == 0)
//                    {
//                        return NoContent(); // if the task list is empty we send a 204 no content
//                    }
//                    else
//                    {
//                        return NoContent(); // if the task list is empty we send a 204 no content
//                    }

//                }
//                catch(Exception ex)
//                {
//                    Console.WriteLine("Error in tasking");
//                    Console.WriteLine(ex.Message);
//                    Console.WriteLine(ex.StackTrace);
//                    return BadRequest();
//                }

//                }
//            catch (Exception e)
//            {
//                Console.WriteLine(e.Message);
//                Console.WriteLine(e.StackTrace);
//                return BadRequest();
//            }
//        }
//}