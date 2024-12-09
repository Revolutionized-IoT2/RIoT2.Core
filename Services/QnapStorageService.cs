using RIoT2.Core.Models;
using RIoT2.Core.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Xml;
using static System.Net.Mime.MediaTypeNames;
using RIoT2.Core.Utils;
using RIoT2.Core;

namespace RIoT2.Common.Services
{
    public class QnapStorageService : IStorageService
    {
        private string _ipAddress;
        private string _rootFolder;
        private string _username;
        private string _password;
        private ILogger<QnapStorageService> _logger;
        private string _sid;

        public QnapStorageService(ILogger<QnapStorageService> logger)
        {
            _logger = logger;
        }

        public void Configure(string username, string password, string rootFolder, string ipAddress)
        {
            _ipAddress = ipAddress;
            _rootFolder = rootFolder;
            _username = username;
            _password = password;
        }

        public async Task Delete(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return;

            var cmd = $"http://{_ipAddress}:8080/cgi-bin/filemanager/utilRequest.cgi?func=delete&sid={getSID()}&path={_rootFolder}&file_total=1&file_name={filename}";

            try
            {
                var response = await Web.Instance.GetAsync(cmd);
                var content = await response.Content.ReadAsStringAsync();
                var result = Json.Instance.Deserialize<QNAPResult>(content);
                if (result.Status != 1)
                    throw new Exception($"Could not delete file. Delete result: {content}");
            }
            catch (Exception x)
            {
                _logger.LogError("Error in Delete", x);
            }
        }

        public async Task<Document> Get(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return null;

            var cmdMeta = $"http://{_ipAddress}:8080/cgi-bin/filemanager/utilRequest.cgi?func=stat&sid={getSID()}&path={_rootFolder}&file_total=1&file_name={filename}";
            var cmdData = $"http://{_ipAddress}:8080/cgi-bin/filemanager/utilRequest.cgi?func=download&sid={getSID()}&isfolder=0&compress=0&source_path={_rootFolder}&source_file={filename}&source_total=1";

            try
            {
                var responseMeta = Web.Instance.GetAsync(cmdMeta);
                var responseData = Web.Instance.GetAsync(cmdData);
                Task.WaitAll(responseMeta, responseData);

                if (!responseMeta.Result.IsSuccessStatusCode || !responseData.Result.IsSuccessStatusCode)
                    throw new Exception("Error downloading file");

                QNAPFolderResult metaResult = Json.Instance.Deserialize<QNAPFolderResult>(await responseMeta.Result.Content.ReadAsStringAsync());
                var metadata = metaResult.datas[0];

                Document document = (Document)metadata;
                document.Data = await responseData.Result.Content.ReadAsByteArrayAsync();

                return document;
            }
            catch (Exception x)
            {
                _logger.LogError("Error in Delete", x);
            }
            return null;
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_ipAddress);
        }

        public async Task<List<DocumentMetadata>> List()
        {
            var cmd = $"http://{_ipAddress}:8080/cgi-bin/filemanager/utilRequest.cgi?func=get_list&sid={getSID()}&is_iso=0&list_mode=all&path={_rootFolder}&dir=ASC&limit=1000&sort=filename&start=0 ";
            try
            {
                var response = await Web.GetAsync(cmd);
                var content = await response.Content.ReadAsStringAsync();
                var result = Json.Deserialize<QNAPFolderResult>(content);
                return result.datas.Where(x => x.Isfolder == FileOrFolder.File).ToList();
            }
            catch (Exception x)
            {
                _logger.LogError("Error in Delete", x);
            }
            return null;
        }

        public async Task Save(string filename, byte[] data)
        {
            string progress = (_rootFolder + "/" + filename).Replace('/', '-');
            string cmd = $"http://{_ipAddress}:8080/cgi-bin/filemanager/utilRequest.cgi?func=upload&type=standard&sid={getSID().Result}&dest_path={_rootFolder}&overwrite=1&progress={progress}";
            try
            {
                var response = await Web.PostMultipartAsync(cmd, data);
                var content = await response.Content.ReadAsStringAsync();
                var result = Json.Deserialize<QNAPResult>(content);
                if (result.Status != 1)
                    throw new Exception($"Could not upload file. Delete result: {content}");
            }
            catch (Exception x)
            {
                _logger.LogError("Error in Delete", x);
            }
        }

        private async Task<string> getSID()
        {
            if (!string.IsNullOrEmpty(_sid))
                return _sid;

            var plainTextBytes = Encoding.UTF8.GetBytes(_password);
            var encodedPw = Convert.ToBase64String(plainTextBytes);
            //encodedPw = System.Web.HttpUtility.UrlEncode(encodedPw);
            var content = $"user={_username}&serviceKey=1&pwd={encodedPw}";

            string url = $"http://{_ipAddress}:8080/cgi-bin/authLogin.cgi";
            try
            {
                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("content-type", "application/x-www-form-urlencoded");

                var response = await Web.PostAsync(url, content, headers);
                string xml = await response.Content.ReadAsStringAsync();

                XmlDocument xd = new XmlDocument();
                xd.LoadXml(xml);
                _sid = xd.SelectSingleNode("/QDocRoot/authSid").InnerText;
            }
            catch (Exception x)
            {
                _logger.LogError("Error authenticating to QNAP", x);
            }
            return _sid;
        }

        //private const string EzEncodechars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        /*
        private string utf16to8(string input)
        {
            byte[] utf16Bytes = Encoding.Unicode.GetBytes(input);
            byte[] utf8Bytes = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, utf16Bytes);
            char[] chars = (char[])Array.CreateInstance(typeof(char), utf8Bytes.Length);

            for (int i = 0; i < utf8Bytes.Length; i++)
                chars[i] = BitConverter.ToChar(new byte[2] { utf8Bytes[i], 0 }, 0);

            return new string(chars);
        }

        private string encode(string input)
        {
            var utf8Input = utf16to8(input);
            var plainTextBytes = Encoding.UTF8.GetBytes(utf8Input);
            return Convert.ToBase64String(plainTextBytes);
        }*/
    }

    public class QNAPFolderResult
    {
        public int total { get; set; }
        public int real_total { get; set; }
        public List<DocumentMetadata> datas { get; set; }
    }

    public class QNAPResult
    {
        public int Status { get; set; }
        public string Success { get; set; }
    }
}
