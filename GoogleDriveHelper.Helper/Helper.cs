﻿using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GoogleDriveHelper.Helper
{
    class Helper
    {
        // If modifying these scopes, delete your previously saved credentials
        // at ~/.credentials/drive-dotnet-quickstart.json
        static string ApplicationName = "US PWC AppScript Test";
        static UserCredential credential;
        static string[] scopes = { DriveService.Scope.Drive, DriveService.Scope.DriveFile, DriveService.Scope.DriveAppdata };

        /// <summary>
        /// authenticate Google Drive service
        /// </summary>
        /// <param name="Scopes"></param>
        /// <returns></returns>
        public static DriveService getDriveService(string[] Scopes)
        {
            using (var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = Environment.GetFolderPath(
                    Environment.SpecialFolder.Personal);
                credPath = Path.Combine(credPath, ".credentials/drive-dotnet-quickstart.json");

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    Environment.UserName,
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
            return service;
        }

        public static bool Authorize(string credentialFilePath)
        {
            bool status = false;
            try
            {
                DriveService service = getDriveService(scopes);
                status = true;
            }
            catch(Exception e)
            {
                status = false;
            }
            return status;
        }
            
        /// <summary>
        /// get file/folder on the Google Drive folder
        /// </summary>
        /// <param name="service"></param>
        /// <param name="parentId"></param>
        /// <returns></returns>
        public static IList<Google.Apis.Drive.v3.Data.File> getFileList(string parentId)
        {
            DriveService service = getDriveService(scopes);
            FilesResource.ListRequest listRequest = service.Files.List();
            listRequest.Q = "'" + parentId + "' in parents";
            listRequest.PageSize = 1000;
            listRequest.Fields = "nextPageToken, files(id,name,trashed,modifiedTime)";

            IList<Google.Apis.Drive.v3.Data.File> files = listRequest.Execute().Files.Where(f => (f.Trashed == false )).OrderBy(x => x.ModifiedTime).ToList();
            return files;
        }

        /// <summary>
        /// add a folder on the Google Drive,return a folder Id
        /// </summary>
        /// <param name="service">Google Drive Service</param>
        /// <param name="folderName"> folder's name</param>
        /// <param name="parentId">the parent folder's Id on the Google Drive</param>
        /// <returns></returns>
        public static string addFolder(string folderName, string parentId)
        {
            DriveService service = getDriveService(scopes);
            string fieldId = "";

            var fileMetadata = String.IsNullOrWhiteSpace(parentId) ?
                new Google.Apis.Drive.v3.Data.File()
                {
                    Name = folderName,
                    MimeType = "application/vnd.google-apps.folder",
                } :
                new Google.Apis.Drive.v3.Data.File()
                {
                    Name = folderName,
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = new List<String>() { parentId },
                };
            var request = service.Files.Create(fileMetadata);
            request.Fields = "id";
            var file = request.Execute();
            fieldId = file.Id;
            return fieldId;
        }

        /// <summary>
        /// upload a folder to the Google Drive
        /// </summary>
        /// <param name="service"></param>
        /// <param name="googleDriveParentFolderId"></param>
        /// <param name="localFolderPath"></param>
        public static void uploadFolder( string googleDriveParentFolderId, string localFolderPath)
        {
            DriveService service = getDriveService(scopes);
            string folderName = localFolderPath.Split(@"\".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            string parentId = addFolder(folderName, googleDriveParentFolderId);
            if (Directory.GetFiles(localFolderPath).Count() > 0)
                foreach (var file in Directory.GetFiles(localFolderPath))
                    uploadFile(file,parentId);
            if (Directory.GetDirectories(localFolderPath).Count() > 0)
                foreach (var subDir in Directory.GetDirectories(localFolderPath))
                    uploadFolder(parentId, subDir);
        }

        /// <summary>
        /// upload a file to the Google Drive
        /// </summary>
        /// <param name="service"></param>
        /// <param name="filePath"></param>
        /// <param name="parentId"></param>
        public static void uploadFile( string filePath, string parentId)
        {
            DriveService service = getDriveService(scopes);
            string fileName = filePath.Split(@"\".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = fileName,
                Parents = new List<String>() { parentId},
            };

            FilesResource.CreateMediaUpload request;
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                request = service.Files.Create(fileMetadata, stream, "");
                request.Fields = "Id";
                request.Upload();
            }
        }

        /// <summary>
        /// print each folder/file name on the Google Drive
        /// </summary>
        /// <param name="fileLists"></param>
        public static void printFileName(IList<Google.Apis.Drive.v3.Data.File> fileLists)
        {
            DriveService service = getDriveService(scopes);
            if (fileLists != null && fileLists.Count > 0)
                foreach (var file in fileLists)
                {
                    Google.Apis.Drive.v3.Data.File f = service.Files.Get(file.Id).Execute();
                    Console.WriteLine("{0} {1}",f.Name, f.MimeType);
                }
            else
                Console.WriteLine("No files found.");
            Console.ReadKey();
        }

        /// <summary>
        /// delete the specific file with the given ID
        /// </summary>
        /// <param name="service"></param>
        /// <param name="fileId"></param>
        public static string deleteFile(string fileId)
        {
            string result = "";
            string fileName = "";
            try
            {
                DriveService service = getDriveService(scopes);
                var request = service.Files.Get(fileId);
                fileName = request.Execute().Name;
                service.Files.Delete(fileId).Execute();
                result = $"Delete file {fileName} successfully.";
            }
            catch (Exception e)
            {
                result = string.IsNullOrWhiteSpace(fileName) ? $"Delete file failed.{e.ToString()}" : $"Delete file {fileName} failed.{e.ToString()}";
            }
            return result;
        }

        /// <summary>
        /// download files from Google Drive
        /// </summary>
        /// <param name="service"></param>
        /// <param name="fileId"></param>
        /// <param name="downloadPath"></param>
        /// <returns></returns>
        public static string downloadFile( string fileId, string downloadPath)
        {
            DriveService service = getDriveService(scopes);
            var downloadStatus = "";
            var request = service.Files.Get(fileId);
            string fileName = request.Execute().Name;
            string parentFolderPath = downloadPath + @"\";
            if (!Directory.Exists(parentFolderPath))
                Directory.CreateDirectory(parentFolderPath);
            string filePath = Path.Combine(parentFolderPath, fileName);

            using(var stream = new FileStream(filePath, FileMode.Create))
            {
                request.MediaDownloader.ProgressChanged +=
                    (IDownloadProgress progress) =>
                    {
                        switch (progress.Status)
                        {
                            case DownloadStatus.Downloading:
                                {
                                    Console.WriteLine(progress.BytesDownloaded);
                                    break;
                                }
                            case DownloadStatus.Completed:
                                {
                                    downloadStatus = "Download complete";
                                    break;
                                }
                            case DownloadStatus.Failed:
                                {
                                    downloadStatus = "Download failed";
                                    break;
                                }
                        }
                    };
                request.Download(stream);
            }
            return downloadStatus;
        }

        /// <summary>
        /// download datas including its subfolders' datas with a given folder ID
        /// </summary>
        /// <param name="service"></param>
        /// <param name="parentId"></param>
        /// <param name="folderName"></param>
        /// <param name="downloadPath"></param>
        public static void downloadFolder( string parentId, string downloadPath)
        {
            DriveService service = getDriveService(scopes);
            var folder = service.Files.Get(parentId).Execute();
            string folderName = folder.Name;

            string parentFolderPath = downloadPath + @"\" + folderName + @"\";
            IList<Google.Apis.Drive.v3.Data.File> listFiles = getFileList(parentId);

            foreach(var file in listFiles)
            {
                var request = service.Files.Get(file.Id);
                string fileName = request.Execute().Name;
                string mimeType = request.Execute().MimeType;
                
                if (!Directory.Exists(parentFolderPath))
                    Directory.CreateDirectory(parentFolderPath);

                string filePath = Path.Combine(parentFolderPath, fileName);
                //Console.WriteLine("_________file name__{0}__MimeType__{1}____", fileName, mimeType);
                if (mimeType.Equals("application/vnd.google-apps.folder"))   //If it's folder
                {
                    Console.WriteLine(Path.Combine(parentFolderPath, file.Name));
                    if(!Directory.Exists(Path.Combine(parentFolderPath, file.Name)))
                        Directory.CreateDirectory(Path.Combine(parentFolderPath, file.Name));
                    downloadFolder(file.Id, parentFolderPath);
                }
                else {
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        request.MediaDownloader.ProgressChanged +=
                            (IDownloadProgress progress) =>
                            {
                                switch (progress.Status)
                                {
                                    case DownloadStatus.Downloading:
                                        {
                                            Console.WriteLine(progress.BytesDownloaded);
                                            break;
                                        }
                                    case DownloadStatus.Completed:
                                        {
                                            Console.WriteLine("file {0} downloaded successfully.", file.Name);
                                            break;
                                        }
                                    case DownloadStatus.Failed:
                                        {
                                            Console.WriteLine("file {0} downloaded faild.", file.Name);
                                            break;
                                        }
                                }
                            };
                        request.Download(stream);
                    }
                }
            }
        }

        /// <summary>
        /// move the file to other folder
        /// </summary>
        /// <param name="service"></param>
        /// <param name="fileId"></param>
        /// <param name="destinationFolderId"></param>
        public static void moveFile( string fileId, string destinationFolderId)
        {
            DriveService service = getDriveService(scopes);
            var getRequest = service.Files.Get(fileId);
            getRequest.Fields = "parents";
            var file = getRequest.Execute();
            var previousParents = String.Join(",", file.Parents);

            var updateRequest = service.Files.Update(new Google.Apis.Drive.v3.Data.File(), fileId);
            updateRequest.Fields = "id, parents";
            updateRequest.AddParents = destinationFolderId;
            updateRequest.RemoveParents = previousParents;
            file = updateRequest.Execute();

        }

        static void Main(string[] args)
        {
            string parentId = "1-d-UWvchZoovjRq5_RbxWzvUJB5IJe1P";
            string credPath = Environment.GetFolderPath(
                    Environment.SpecialFolder.Personal);
            credPath = Path.Combine(credPath, ".credentials/drive-dotnet-quickstart.json");
     
            if (Authorize(credPath))
            {
                //IList<Google.Apis.Drive.v3.Data.File> fileLists = getFileList(parentId);
                //printFileName(fileLists);

                //string localFolderPath = @"C:\Users\vlei002\Desktop\vivi\Study\CODE\GoogleDriveTest\Test\Properties";
                //uploadFolder(parentId, localFolderPath);

                //string localFilePath = @"C:\Users\vlei002\Desktop\vivi\Project\UIPath\FATCA Robot\0531\FATCA Robot\Custom Package\GoogleDriveAPIActivities.1.0.1.1.nupkg";
                //uploadFile(localFilePath, parentId);

                Console.WriteLine(deleteFile("1rSljBkgF7qt-oT08NitcrzbjcAei1aLn"));

                //string fileId = "1QAwNnc2Kj6YWGiRDFoHkgAsQAsAlFNCh";
                //downloadFile(fileId, localFolderPath);

                //parentId = "1MHifp0srO0bFnFT_ZECHcnR_HQkvEoHR";
                //string folderName = "abc";
                //string downloadPath = @"C:\Users\vlei002\Desktop\vivi\Study\CODE";
                //downloadFolder(parentId, downloadPath);

                //parentId = "1KHeEkXVXaKhq_SAGcqdTbt1ZdbVw_4FI";
                //string destinationFolderId = "1TYmz-Q5grxMxQMoUsN_FBtShj9hPlXAA";
                //moveFile(parentId, destinationFolderId);

                //Console.ReadKey();
            }
        }
    }
}