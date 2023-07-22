using JenkinsNET.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Web;

namespace JenkinsNET.Internal.Commands
{
    internal class JobBuildWithParametersCommand : JenkinsHttpCommand
    {
        public JenkinsBuildResult Result {get; internal set;}

        public JobBuildWithParametersCommand(IJenkinsContext context, string jobName, IDictionary<string, string> jobParameters, IDictionary<string, string> jobFileParameters=null)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (string.IsNullOrEmpty(jobName))
                throw new ArgumentException("'jobName' cannot be empty!");

            if (jobParameters == null)
                throw new ArgumentNullException(nameof(jobParameters));

            var _params = new Dictionary<string, string>(jobParameters) {
                ["delay"] = "0sec",
            };



            var query = new StringWriter();
            WriteJobParameters(query, _params);

            Url = NetPath.Combine(context.BaseUrl, "job", jobName, $"buildWithParameters?{query}");
            UserName = context.UserName;
            Password = context.Password;
            Crumb = context.Crumb;

            OnWrite = request => {
                request.Method = "POST";
                if (jobFileParameters != null)
                {
                    string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
                    byte[] boundaryBytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

                    request.ContentType = "multipart/form-data; boundary=" + boundary;
                    request.KeepAlive = true;

                    using (Stream requestStream = request.GetRequestStream())
                    {
                        foreach (KeyValuePair<string, string> pair in jobFileParameters)
                        {

                            requestStream.Write(boundaryBytes, 0, boundaryBytes.Length);

                            string header = "Content-Disposition: form-data; name=\"" + pair.Key + "\"; filename=\"" + pair.Value + "\r\n\r\n";
                            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(header);
                            requestStream.Write(bytes, 0, bytes.Length);
                            byte[] buffer = new byte[32768];
                            int bytesRead;
                            using (FileStream fileStream = File.OpenRead(pair.Value))
                            {
                                while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                                    requestStream.Write(buffer, 0, bytesRead);
                            }

                        }

                        byte[] footer = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
                        requestStream.Write(footer, 0, footer.Length);
                    }


                }
            };

            OnRead = response => {
                if (response.StatusCode != System.Net.HttpStatusCode.Created)
                    throw new JenkinsJobBuildException($"Expected HTTP status code 201 but found {(int)response.StatusCode}!");

                Result = new JenkinsBuildResult {
                    QueueItemUrl = response.GetResponseHeader("Location"),
                };
            };

        #if NET_ASYNC
            OnWriteAsync = async (request, token) => {
                request.Method = "POST";
            };

            OnReadAsync = async (response, token) => {
                if (response.StatusCode != System.Net.HttpStatusCode.Created)
                    throw new JenkinsJobBuildException($"Expected HTTP status code 201 but found {(int)response.StatusCode}!");

                Result = new JenkinsBuildResult {
                    QueueItemUrl = response.GetResponseHeader("Location"),
                };
            };
        #endif
        }

        private void WriteJobParameters(TextWriter writer, IDictionary<string, string> jobParameters)
        {
            var isFirst = true;
            foreach (var pair in jobParameters) {
                if (isFirst) {
                    isFirst = false;
                }
                else {
                    writer.Write('&');
                }

                var encodedName = HttpUtility.UrlEncode(pair.Key);
                var encodedValue = HttpUtility.UrlEncode(pair.Value);

                writer.Write(encodedName);
                writer.Write('=');
                writer.Write(encodedValue);
            }
        }
    }
}
