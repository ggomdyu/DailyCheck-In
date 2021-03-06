﻿using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace IchikaDailyJanken
{
    public class NetworkRequestData
    {
        public string url;
        public byte[] body;
        public string method;
        public Dictionary<string, string> headers;
        public Action<byte[]> callback;
    }

    public class NetworkRequestHelper
    {
        #region Property
        public static NetworkRequestHelper Instance { get; } = new NetworkRequestHelper();
        #endregion

        #region Variable
        private ConcurrentQueue<Tuple<byte[], Action<byte[]>>> m_webRequestResultQueue = new ConcurrentQueue<Tuple<byte[], Action<byte[]>>>();
        private CookieContainer m_cookieContainer = new CookieContainer();

        public Action<NetworkRequestData> OnRequestFailure;
        #endregion

        #region Method
        public void Update()
        {
            while (true)
            {
                if (m_webRequestResultQueue.TryDequeue(out var item) == false)
                {
                    break;
                }

                item.Item2(item.Item1);
            }
        }

        public void Get(string url, Action<byte[]> callback)
        {
            Get(url, null, callback);
        }

        public void Get(string url, Dictionary<string, string> headers, Action<byte[]> callback)
        {
            var networkRequestData = new NetworkRequestData
            {
                url = url,
                body = null,
                method = "GET",
                headers = headers,
                callback = callback
            };

            try
            {
                HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                request.Method = "GET";
                request.CookieContainer = m_cookieContainer;
                ParseHeader(request, headers);

                request.BeginGetResponse(new AsyncCallback(OnFinishWebRequest), new Tuple<HttpWebRequest, NetworkRequestData>(request, networkRequestData));
            }
            catch (Exception e)
            {
                OnRequestFailure?.Invoke(networkRequestData);
            }
        }

        public void Post(string url, byte[] body, Action<byte[]> callback)
        {
            Post(url, body, null, callback);
        }

        public void Post(string url, byte[] body, Dictionary<string, string> headers, Action<byte[]> callback)
        {
            var networkRequestData = new NetworkRequestData
            {
                url = url,
                body = body,
                method = "POST",
                headers = headers,
                callback = callback
            };

            try
            {
                var request = WebRequest.Create(url) as HttpWebRequest;
                request.Method = "POST";
                request.CookieContainer = m_cookieContainer;
                ParseHeader(request, headers);

                if (body != null)
                {
                    request.ContentLength = body.Length;
                    Stream requestStream = request.GetRequestStream();
                    requestStream.Write(body, 0, body.Length);
                }

                request.BeginGetResponse(OnFinishWebRequest, new Tuple<HttpWebRequest, NetworkRequestData>(request, networkRequestData));
            }
            catch (Exception e)
            {
                OnRequestFailure?.Invoke(networkRequestData);
            }
        }

        void OnFinishWebRequest(IAsyncResult result)
        {
            var param = result.AsyncState as Tuple<HttpWebRequest, NetworkRequestData>;
            if (param == null)
            {
                return;
            }

            HttpWebResponse response = null;
            try
            {
                response = param.Item1.EndGetResponse(result) as HttpWebResponse;
            }
            catch (Exception e)
            {
                m_webRequestResultQueue.Enqueue(new Tuple<byte[], Action<byte[]>>(null, (param2) =>
                {
                    OnRequestFailure?.Invoke(param.Item2);
                }));

                return;
            }

            using (Stream stream = response.GetResponseStream())
            {
                var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);

                m_webRequestResultQueue.Enqueue(new Tuple<byte[], Action<byte[]>>(memoryStream.ToArray(), param.Item2.callback));
            }
        }

        void ParseHeader(HttpWebRequest request, Dictionary<string, string> headers)
        {
            if (headers == null)
            {
                return;
            }
            if (headers.TryGetValue("Content-Type", out var contentType))
            {
                request.ContentType = contentType;
            }

            if (headers.TryGetValue("Keep-Alive", out var keepAlive))
            {
                request.KeepAlive = keepAlive == "true" ? true : false;
            }

            if (headers.TryGetValue("User-Agent", out var userAgent))
            {
                request.UserAgent = userAgent;
            }

            if (headers.TryGetValue("Accept", out var accept))
            {
                request.Accept = accept;
            }

            if (headers.TryGetValue("Host", out var host))
            {
                request.Host = host;
            }
        }

        #endregion
    }

    class EAmusementCaptchaSolver
    {
        #region Enum
        private enum CharacterType
        {
            Unknown,
            Bomberman,
            Girl,
            Goemon,
            Rabbit,
            Robot,
        }
        #endregion

        #region Variable
        private string m_mainCharacterImageUrl;
        private string[] m_subCharacterImageUrls;
        private readonly Color m_rabbitIdentifierColor = Color.FromArgb(255, 0, 220, 170);
        private readonly Color m_robotIdentifierColor = Color.FromArgb(255, 40, 90, 180);
        private readonly Color m_girlIdentifierColor = Color.FromArgb(255, 240, 90, 145);
        private readonly Color m_bombermanIdentifierColor = Color.FromArgb(255, 255, 225, 170);
        private readonly Color m_goemonIdentifierColor = Color.FromArgb(255, 230, 55, 35);
        #endregion

        #region Constructor
        public EAmusementCaptchaSolver(string mainCharacterImageUrl, string[] subCharacterImageUrls)
        {
            m_mainCharacterImageUrl = mainCharacterImageUrl;
            m_subCharacterImageUrls = subCharacterImageUrls;
        }
        #endregion

        #region Method
        public List<int> SolveProblem()
        {
            // 1. Download main character image and identify it.
            Task<Bitmap> mainCharacterImage = DownloadImage(m_mainCharacterImageUrl);
            Task.WaitAll(mainCharacterImage);
            var mainCharacterType = GetMainCharacterType(mainCharacterImage.Result);

            // 2. Download sub character images and identify them.
            const int subCharacterImageCount = 5;
            var subCharacterImages = new Task<Bitmap>[subCharacterImageCount];
            for (var i = 0; i < subCharacterImageCount; ++i)
            {
                subCharacterImages[i] = DownloadImage(m_subCharacterImageUrls[i]);
            }

            Task.WaitAll(subCharacterImages);
            if (mainCharacterType == CharacterType.Unknown || Array.FindIndex(subCharacterImages, (item) => item.Result == null) != -1)
            {
                return null;
            }

            return GetMatchedSubCharacterIndices(mainCharacterType, subCharacterImages);
        }

        private async Task<Bitmap> DownloadImage(string url)
        {
            var request = WebRequest.Create(url);
            var response = await request.GetResponseAsync();
            var responseStream = response.GetResponseStream();
            return new Bitmap(responseStream);
        }
        private CharacterType GetMainCharacterType(Bitmap image)
        {
            do
            {
                var characterTypeMatchConditionTable = new List<(Point, Color, CharacterType)>
                {
                    (new Point(42, 19), m_bombermanIdentifierColor, CharacterType.Bomberman),
                    (new Point(55, 8), m_girlIdentifierColor, CharacterType.Girl),
                    (new Point(42, 58), m_goemonIdentifierColor, CharacterType.Goemon),
                    (new Point(48, 11), m_rabbitIdentifierColor, CharacterType.Rabbit),
                    (new Point(53, 22), m_robotIdentifierColor, CharacterType.Robot)
                };

                foreach (var characterTypeMatchCondition in characterTypeMatchConditionTable)
                {
                    if (image.GetPixel(characterTypeMatchCondition.Item1.X, characterTypeMatchCondition.Item1.Y) == characterTypeMatchCondition.Item2)
                    {
                        return characterTypeMatchCondition.Item3;
                    }
                }
            } while (false);

            return CharacterType.Unknown;
        }

        private List<int> GetMatchedSubCharacterIndices(CharacterType mainCharacterType, Task<Bitmap>[] subCharacterImages)
        {
            var characterTypeMatchConditionTable = new Dictionary<CharacterType, (List<Point>, Color)>
            {
                {CharacterType.Bomberman, (new List<Point>{new Point(49, 9), new Point(48, 34), new Point(57, 32), new Point(51, 20), new Point(31, 29)}, m_bombermanIdentifierColor)},
                {CharacterType.Girl, (new List<Point>{new Point(41, 15), new Point(39, 10), new Point(45, 13), new Point(50, 14), new Point(42, 11)}, m_girlIdentifierColor)},
                {CharacterType.Goemon, (new List<Point>{new Point(56, 51), new Point(49, 49), new Point(55, 60), new Point(59, 68), new Point(38, 72)}, m_goemonIdentifierColor)},
                {CharacterType.Rabbit, (new List<Point>{new Point(81, 9), new Point(55, 11), new Point(38, 5), new Point(31, 18), new Point(69, 4)}, m_rabbitIdentifierColor)},
                {CharacterType.Robot, (new List<Point>{new Point(37, 54), new Point(68, 32), new Point(27, 45), new Point(27, 49), new Point(48, 45)}, m_robotIdentifierColor)},
            };

            var matchedSubCharacterIncices = new List<int>();
            var characterTypeMatchCondition = characterTypeMatchConditionTable[mainCharacterType];

            for (int i = 0; i < subCharacterImages.Length; ++i)
            {
                var subCharacterImage = subCharacterImages[i].Result;
                for (int j = 0; j < characterTypeMatchCondition.Item1.Count; ++j)
                {
                    if (subCharacterImage.GetPixel(characterTypeMatchCondition.Item1[j].X, characterTypeMatchCondition.Item1[j].Y) == characterTypeMatchCondition.Item2)
                    {
                        matchedSubCharacterIncices.Add(i);
                        break;
                    }
                }
            }

            return matchedSubCharacterIncices;
        }
        #endregion
    }

    class JubeatWebClient
    {
        #region Enum
        public enum LoginStatus
        {
            Success,
            Failure,
            InvalidEmailOrPassword,
        }

        public enum JankenStatus
        {
            Success,
            Failure,
        }
        #endregion

        #region Variable
        private static Dictionary<string, string> m_commonHeader = new Dictionary<string, string>
        {
            {"Content-Type", "application/json"},
            {"Keep-Alive", "true"},
            {"Upgrade-Insecure-Requests", "1"},
            {"User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.102 Safari/537.36"},
            {"Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8"},
            {"Accept-Encoding", "sdch"},
            {"Accept-Language", "ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7"},
            {"Host", "p.eagate.573.jp"},
        };
        #endregion

        #region Method
        public static void Login(string userId, string userPassword, Action<LoginStatus> onLoginComplete)
        {
            RequestGenerateKcaptcha((response) =>
            {
                var parsedData = ParseKcaptchaJson(Encoding.UTF8.GetString(response));
                if (parsedData == null)
                {
                    onLoginComplete(LoginStatus.Failure);
                    return;
                }

                var choiceCharacterImageUrls = new string[parsedData.Value.choiceCharacterImageUrlKeys.Count];
                for (int i = 0; i < choiceCharacterImageUrls.Length; ++i)
                {
                    var choiceCharacterImageUrlKey = parsedData.Value.choiceCharacterImageUrlKeys[i];
                    choiceCharacterImageUrls[i] = $"https://img-auth.service.konami.net/captcha/pic/{choiceCharacterImageUrlKey}";
                }

                var captchaSolver = new EAmusementCaptchaSolver(parsedData.Value.correctPickCharacterImageUrl, choiceCharacterImageUrls);
                var matchedChoiceCharacterIndices = captchaSolver.SolveProblem();
                if (matchedChoiceCharacterIndices == null)
                {
                    onLoginComplete(LoginStatus.Failure);
                    return;
                }

                // Assemble captcha keys
                int matchedChoiceCharacterIndicesIter = 0;
                string captchaKey = $"k_{parsedData.Value.kcsess}";
                for (int i = 0; i < parsedData.Value.choiceCharacterImageUrlKeys.Count; ++i)
                {
                    captchaKey += "_";

                    if (matchedChoiceCharacterIndices.Count > matchedChoiceCharacterIndicesIter && matchedChoiceCharacterIndices[matchedChoiceCharacterIndicesIter] == i)
                    {
                        captchaKey += parsedData.Value.choiceCharacterImageUrlKeys[i];
                        ++matchedChoiceCharacterIndicesIter;
                    }
                }

                RequestLoginAuth(userId, userPassword, captchaKey, (response2) =>
                {
                    var loginPageJson = JObject.Parse(Encoding.UTF8.GetString(response2));
                    var failCode = loginPageJson["fail_code"].Value<string>();
                    if (int.TryParse(failCode, out var parsedFailCode))
                    {
                        onLoginComplete(parsedFailCode == 0 ? LoginStatus.Success : parsedFailCode == 200 ? LoginStatus.InvalidEmailOrPassword : LoginStatus.Failure);
                    }
                    else
                    {
                        onLoginComplete(LoginStatus.Failure);
                    }
                });
            });
        }

        public static void DoJanken(Action<JankenStatus> onRequestComplete)
        {
            RequestJankenPage((response) =>
            {
                var html = Encoding.UTF8.GetString(response);

                var jankenUrlStartOffset = html.IndexOf("/game/bemani/bjm2020/janken/exe.html?form_type=");
                if (jankenUrlStartOffset == -1)
                {
                    onRequestComplete?.Invoke(JankenStatus.Failure);
                    return;
                }

                var jankenUrlEndOffset = html.IndexOf('"', jankenUrlStartOffset);
                var jankenUrl = "https://p.eagate.573.jp" + html.Substring(jankenUrlStartOffset, jankenUrlEndOffset - jankenUrlStartOffset);
                NetworkRequestHelper.Instance.Post(jankenUrl, null, m_commonHeader, (response2) =>
                {
                    onRequestComplete?.Invoke(JankenStatus.Success);
                });
            });
        }

        private static void RequestJankenPage(Action<byte[]> onRequestComplete)
        {
            NetworkRequestHelper.Instance.Get("https://p.eagate.573.jp/game/bemani/bjm2020/janken/index.html", m_commonHeader, onRequestComplete);
        }

        private static void RequestGenerateKcaptcha(Action<byte[]> onRequestComplete)
        {
            NetworkRequestHelper.Instance.Post("https://p.eagate.573.jp/gate/p/common/login/api/kcaptcha_generate.html", null, m_commonHeader, onRequestComplete);
        }

        private static void RequestLoginAuth(string userEmail, string userPassword, string captchaKey, Action<byte[]> onRequestComplete)
        {
            var url = $"https://p.eagate.573.jp/gate/p/common/login/api/login_auth.html?";
            url += $"login_id={userEmail}";
            url += $"&pass_word={userPassword}";
            url += $"&otp=";
            url += $"&resrv_url={"/gate/p/login_complete.html"}";
            url += $"&captcha={captchaKey}";

            NetworkRequestHelper.Instance.Post(url, null, m_commonHeader, onRequestComplete);
        }

        private static (string kcsess, string correctPickCharacterImageUrl, List<string> choiceCharacterImageUrlKeys)? ParseKcaptchaJson(string kcaptchaJson)
        {
            do
            {
                var kcaptchaJsonDict = JObject.Parse(kcaptchaJson);

                var dataElem = kcaptchaJsonDict["data"];

                // Parse the correct pick character image URL.
                var correctPickCharacterImageUrl = dataElem["correct_pic"].Value<string>();

                // Parse the kcsess value.
                var kcsess = dataElem["kcsess"].Value<string>();

                // Parse the choice list character image URL.
                var choiseListArrayElem = dataElem["choicelist"];
                var choiceCharacterImageUrlKeys = new List<String>();
                foreach (var choiseListElem in choiseListArrayElem.Children())
                {
                    var choiceCharacterImageUrlKey = choiseListElem["key"].Value<string>();
                    if (string.IsNullOrEmpty(choiceCharacterImageUrlKey))
                    {
                        continue;
                    }

                    choiceCharacterImageUrlKeys.Add(choiceCharacterImageUrlKey);
                }

                return (kcsess, correctPickCharacterImageUrl, choiceCharacterImageUrlKeys);
            } while (false);

            return null;
        }
        #endregion
    }

    class IchikaDailyJanken
    {
        static void Main(string[] args)
        {
            NetworkRequestHelper.Instance.OnRequestFailure += (networkRequestData) =>
            {
                Console.WriteLine($"An unexpected error occured! ({networkRequestData.url})");
            };

            bool isJankenComplete = false;
            JubeatWebClient.Login(args[0], args[1], (loginStatus) =>
            {
                if (loginStatus == JubeatWebClient.LoginStatus.Success)
                {
                    JubeatWebClient.DoJanken((jankenStatus) =>
                    {
                        if (jankenStatus == JubeatWebClient.JankenStatus.Success)
                        {
                            Console.WriteLine("Janken request complete!");
                            Thread.Sleep(2000);
                        }
                        else
                        {
                            Console.WriteLine("Janken request failed. Maybe you have already done janken...");
                            Thread.Sleep(5000);
                        }

                        isJankenComplete = true;
                    });
                }
                else if (loginStatus == JubeatWebClient.LoginStatus.InvalidEmailOrPassword)
                {
                    Console.WriteLine("Your email or password is incorrect. Please check it again.");
                    Thread.Sleep(5000);
                    
                    isJankenComplete = true;
                }
            });

            while (isJankenComplete == false)
            {
                NetworkRequestHelper.Instance.Update();
            }
        }
    }
}
