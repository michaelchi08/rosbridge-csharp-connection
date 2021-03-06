﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;

namespace RosBridgeUtility
{
    class MsgOperation
    {
        public String op { get; set; }
    }

    class UnsubscribeMessage : MsgOperation
    {
        public UnsubscribeMessage()
        {
            op = "unsubscribe";
        }
        public UnsubscribeMessage(String _topic)
        {
            op = "unsubscribe";
            topic = _topic;
        }
        public String topic { get; set; }
    }

    class SubscribeMessage : UnsubscribeMessage
    {
        public int throttle_rate { get; set; }

        public SubscribeMessage()
        {
            op = "subscribe";
            throttle_rate = 0;
        }
        public SubscribeMessage(String _topic, int throttle = 0)
        {
            op = "subscribe";
            topic = _topic;
            throttle_rate = throttle;
        }
    }

    class PublishMessage : UnsubscribeMessage
    {
        public PublishMessage(String _topic)
            : base(_topic)
        {
            op = "publish";
        }
        public JObject msg { get; set; }
    }    

    public class RosBridgeLogic
    {
        WebSocket ws;
        JObject lastMessage;
        IROSBridgeController subject;
        List<Tuple<String, String>> CollectedData;
        Boolean connected;
        // Velocity threshold values
        double velocity_increment = 0.1;
        double max_velocity = 2.0;
        double min_velocity = -2.0;
        public double current_velocity { get; set; }
        // Angular velocity threshold values
        // In degrees (yet)
        public VelocityState currentVelocityState { get; set; }
        public double angVelocity_increment { get; set; }
        public double current_angVelocity { get; set; }
        double min_ang = 0;
        double max_ang = 0;

        public String currentTarget { get; set; }

        public delegate void MonitoringStoppedHandler(object sender, EventArgs e);
        public event EventHandler MonitoringStopped;

        public RosBridgeLogic()
        {
            currentVelocityState = new VelocityState();
        }

        public void OnMonitoringStopped()
        {
            if (MonitoringStopped != null)
            {
                MonitoringStopped(this, EventArgs.Empty);
            }
            else
            {
                Console.Out.WriteLine("Monitoring stopped");
            }
        }

        public bool isConnected()
        {
            return connected;
        }

        public void SetUpdateListener()
        {
            ws.OnMessage += UpdateOnReceive;
        }

        public void AddStoppedListener(EventHandler mon1)
        {
            MonitoringStopped += mon1;
        }

        public void Initialize(String ipaddress)
        {
            ws = new WebSocket(ipaddress);         
        }

        public void Initialize(String ipaddress, IROSBridgeController parentwindow)
        {
            Console.WriteLine(current_velocity);
            try
            {
                ws = new WebSocket(ipaddress);
                subject = parentwindow;
                ws.OnMessage += UpdateOnReceive;
                Console.Out.WriteLine("Initialized RosBridgeLogic component");
                connected = false;
            }
            catch (Exception se)
            {
                throw se;
            }
        }

        public void initVelocityThreshold(double min_vel, double max_vel,
            double inc_vel, double init_vel)
        {
            max_velocity = max_vel;
            min_velocity = min_vel;
            velocity_increment = inc_vel;
            current_velocity = init_vel;
            Console.WriteLine(current_velocity);
        }

        public void initAngularVelocityThreshold(double min_ang, double max_ang,
            double inc_ang, double init_ang)
        {
            this.max_ang = degToRad(max_ang);
            this.min_ang = degToRad(min_ang);
            angVelocity_increment = degToRad(inc_ang);
            this.current_angVelocity = degToRad(init_ang);
        }

        public Boolean getConnectionState()
        {
            return connected;
        }

        public void Connect()
        {
            try
            {
                ws.OnError += ws_OnError;
                connected = true;
                ws.Connect();                
                Console.Out.WriteLine("Successfully connected to: {0}", ws.Url);
            }
            catch (Exception se)
            {
                throw;
            }
        }

        public void ws_OnError(object sender, ErrorEventArgs e)
        {            
            Console.WriteLine("An error occured: {0}", e.Message);
            connected = false;
        }

        public void Disconnect()
        {
            if (ws.ReadyState == WebSocketState.Open)
            {
                ws.Close();
                connected = false;
            }
        }

        public void CollectData(Object Sender, MessageEventArgs a)
        {
            JObject jData = JObject.Parse(a.Data);
            CollectedData.Add(new Tuple<String, String>(jData["topic"].ToString(),
                jData["msg"].ToString()));

        }

        public void InitializeCollection()
        {
            CollectedData = new List<Tuple<string,string>>();
            ws.OnMessage += CollectData;
        }

        public void StartCollection(String topic, int throttle=0)
        {
            sendSubscription(topic, throttle);
        }

        public void RemoveCollection(String topic)
        {
            sendUnsubscribe(topic);       
        }

        public List<Tuple<String, String>> StopCollection()
        {
            OnMonitoringStopped();
            ws.OnMessage -= CollectData;
            return CollectedData;
        }

        public void sendSubscription(String topic, int throttle = 0)
        {
            SubscribeMessage sub1 = new SubscribeMessage(topic, throttle);
            ws.Send(JsonConvert.SerializeObject(sub1).ToString());
        }

        public void sendUnsubscribe(String topic)
        {
            UnsubscribeMessage mes1 = new UnsubscribeMessage(topic);
            ws.Send(JsonConvert.SerializeObject(mes1).ToString());
        }

        public void ReceiveData(Object Sender, MessageEventArgs e)
        {
            lastMessage = JObject.Parse(e.Data);
        }

        public void sendPublish(String topic, String msg)
        {
            PublishMessage pub1 = new PublishMessage(topic);
            JObject jsondata = JObject.Parse(JsonConvert.SerializeObject(pub1));            
            jsondata["msg"] = msg;
            Console.Out.WriteLine(jsondata.ToString());
            ws.Send(jsondata.ToString());
        }

        public void sendPublish(String topic, JObject msg)
        {
            PublishMessage pub1 = new PublishMessage(topic);
            JObject jsondata = JObject.Parse(JsonConvert.SerializeObject(pub1));
            jsondata["msg"] = msg;
            ws.Send(jsondata.ToString());
        }

        public void setSubject(IROSBridgeController _subject)
        {
            this.subject = _subject;
        }

        public void UpdateOnReceive(Object Sender, MessageEventArgs e)
        {
            subject.ReceiveUpdate(e.Data);
        }

        public String getUrl()
        {
            return ws.Url.ToString();
        }

        public void AddMessageEventListener(EventHandler<WebSocketSharp.MessageEventArgs> a)
        {
            ws.OnMessage += a;
        }

        public List<T> projectionResponse<T>(String topic, String field)
        {
            List<T> resp = new List<T>();
            var converter = TypeDescriptor.GetConverter(typeof(T));
            foreach (var item in CollectedData.Where(
                s => s.Item1.Equals("\""+topic+"\"")))
            {
                JObject firstElement = JObject.Parse(item.Item2);
                resp.Add((T)converter.ConvertFromString(null, 
                    CultureInfo.CreateSpecificCulture("en-US"), 
                    (firstElement.SelectToken(field)).ToString()));                
            }
            return resp;
        }

        public List<List<T>> projectionResponseList<T>(String topic, String field)
        {
            List<List<T>> resp = new List<List<T>>();
            var converter = TypeDescriptor.GetConverter(typeof(T));
            foreach (var item in CollectedData.Where(
                s => s.Item1.Equals("\"" + topic + "\"")))
            {
                JArray firstElement = (JArray)JObject.Parse(item.Item2).SelectToken(field);
                List<T> resp1 = new List<T>();
                foreach (var item2 in firstElement.ToList())
                {
                    resp1.Add((T)converter.ConvertFromString(null,
                        CultureInfo.CreateSpecificCulture("en-US"),
                        item2.ToString()));
                }
                resp.Add(resp1);
            }            
            return resp;
        }
        
        public System.Collections.IList getResponseAttribute(String topic, String field)
        {
            try
            {
                JObject firstElement = JObject.Parse(
                    CollectedData.First(s => s.Item1.Equals("\"" + topic + "\"")).Item2);
                bool test;
                int testInt;
                double testDouble;
                if (Boolean.TryParse((firstElement.SelectToken(field)).ToString(), out test))
                {
                    return projectionResponse<Boolean>(topic, field);
                }
                else if (Int32.TryParse((firstElement.SelectToken(field)).ToString(), out testInt))
                {
                    return projectionResponse<Int32>(topic, field);
                }
                else if (Double.TryParse((firstElement.SelectToken(field)).ToString(),
                    NumberStyles.Number, CultureInfo.CreateSpecificCulture("en-US"),
                    out testDouble))
                {
                    return projectionResponse<Double>(topic, field);
                }
                else if (firstElement.SelectToken(field) is JArray)
                {
                    return projectionResponseList<Double>(topic, field);

                }
                else
                {
                    return projectionResponse<String>(topic, field);
                }
            }
            catch (Exception se)
            {
                throw se;
            }
        }

        public void StartCollections(IList<TopicObject> subscriptions)
        {
            foreach (var item in subscriptions)
            {
                this.StartCollection(item.name, item.throttle);
            }
        }

        public void RemoveCollections(IList<TopicObject> subscriptions)
        {
            foreach (var item in subscriptions)
            {
                this.RemoveCollection(item.name);
            }
        }

        public void PublishMessage(String topic, String [] keys, Object[] vals)
        {
            JObject msgData = new JObject();
            for (int i = 0; i < keys.Length; i++)
            {
                if (vals[i] is double)
                {
                    msgData[keys[i]] = (Double)vals[i];
                }
                else if (vals[i] is Int64)
                {
                    msgData[keys[i]] = (Int64)vals[i];
                }
                else if (vals[i] is IDictionary<String, Double>)
                {
                    msgData[keys[i]] = JObject.Parse(JsonConvert.SerializeObject(vals[i]));
                }
                else if (vals[i] is Array)
                {
                    JArray arr = new JArray(vals[i]);
                    msgData[keys[i]] = arr;
                }
                else
                {
                    bool test;
                    int testInt;
                    double testDouble;
                    if (Boolean.TryParse((vals[i]).ToString(), out test))
                    {
                        msgData[keys[i]] = test;
                    }
                    else if (Double.TryParse((vals[i]).ToString(),
                    NumberStyles.Number, CultureInfo.CreateSpecificCulture("en-US"),
                    out testDouble))
                    {
                        msgData[keys[i]] = testDouble;
                    }
                    else if (Int32.TryParse((vals[i]).ToString(), out testInt))
                    {
                        msgData[keys[i]] = testInt;
                    }
                    else
                    {
                        msgData[keys[i]] = vals[i].ToString();
                    }
                }
            }
            Console.Out.WriteLine(msgData.ToString());
            sendPublish(topic, msgData);
        }

        public void PublishTwistMsg(String topic, Object[] linear, Object[] angular)
        {
            String[] keys = { "linear", "angular" };
            Dictionary<String, double> lin = new Dictionary<string, double>()
            {
                {"x",(Double)linear[0]},
                {"y",(Double)linear[1]},
                {"z",(Double)linear[2]}
            };
            Dictionary<String, double> ang = new Dictionary<string, double>()
            {
                {"x",(Double)angular[0]},
                {"y",(Double)angular[1]},
                {"z",(Double)angular[2]}
            };
            Object[] vals = { lin, ang };
            PublishMessage(topic, keys, vals);
        }

        public void PublishNeobotixCommandMsg(String topic, Object[] angularVelocity,
            Object[] driveActive, Object[] quickStop, Object[] disableBrake)
        {
            String[] keys = { "angularVelocity", "driveActive", 
                                "quickStop", "disableBrake" };
            Object[] vals = { angularVelocity, 
                                driveActive, quickStop, disableBrake };
            PublishMessage(topic, keys, vals);
        }

        public void moveTarget(double linear, double angular, String target, IList<String> publicationList)
        {
            switch (target)
            {
                case "turtle":
                    PublishturtleMessage(linear, angular, publicationList);
                    break;
                case "neobotix_mp500":
                    PublishNeobotixMsg(linear, angular, publicationList);
                    break;
                case "pr2":
                    PublishPR2BaseControllerMessage(linear, angular, publicationList);
                    break;
                default:
                    throw new Exception("Invalid target: "+target);
            }
        }

        // Teleoperation commands

        public void moveForward(IList<String> publications)
        {
            moveTarget(current_velocity, 0, currentTarget, publications);
            currentVelocityState.current_vel = current_velocity;
        }

        public void moveBackward(IList<String> publications)
        {
            moveTarget(-current_velocity, 0, currentTarget, publications);
            currentVelocityState.current_vel = current_velocity;
        }

        public void moveLeft(IList<String> publications)
        {
            moveTarget(current_velocity, current_angVelocity,
                currentTarget, publications);
            currentVelocityState.current_vel = current_velocity;
        }

        public void moveRight(IList<String> publications)
        {
            moveTarget(current_velocity, current_angVelocity,
                currentTarget, publications);
            currentVelocityState.current_vel = current_velocity;
        }

        public void decreaseVelocity()
        {
            if (current_velocity > min_velocity)
            {
                current_velocity -= velocity_increment;
            }
            currentVelocityState.current_vel = current_velocity;
            
        }

        public double degToRad(double deg)
        {
            return deg * Math.PI / 180.0;
        }

        public void increaseVelocity()
        {
            if (current_velocity < max_velocity)
            {
                current_velocity += velocity_increment;
            }
            currentVelocityState.current_vel = current_velocity;            
        }

        public void increaseAngVelocity()
        {
            if (current_angVelocity < max_ang)
            {
                current_angVelocity += angVelocity_increment;
            }
            currentVelocityState.currentTheta = current_angVelocity;
        }

        public void decreaseAngVelocity()
        {
            if (current_angVelocity > min_ang)
            {
                current_angVelocity -= angVelocity_increment;
            }
            currentVelocityState.currentTheta = current_angVelocity;
        }

        public void setCurrentVelocity(double velocity)
        {
            current_velocity = velocity;
        }

        public void stopTarget(IList<String> publications)
        {
            moveTarget(0, 0, currentTarget, publications);
            currentVelocityState.current_vel = current_velocity;
        }

        private void PublishPR2BaseControllerMessage(double linear, double angular, IList<string> publicationList)
        {
            try
            {
                //for (int i = 0; i < 100; i++)
                //{
                    var v = new { linear = linear, angular = angular };
                    Object[] lin = { linear, 0.0, 0.0 };
                    Object[] ang = { 0.0, 0.0, angular };
                    /*
                    foreach (var item in bridgeConfig.getPublicationList())
                    {
                        this.PublishTwistMsg(item, lin, ang);
                    }
                     * */
                    foreach (var item in publicationList)
                    {
                        this.PublishTwistMsg(item, lin, ang);
                    }                    
                //}

            }
            catch (System.Net.Sockets.SocketException)
            {
                throw new System.Net.Sockets.SocketException(10);
            }
        }
        
        private void PublishNeobotixMsg(double linear, double angular, IList<String> publicationList)
        {
            try
            {
                double r = 0.1275;
                double b = 0.56;
                Object[] linNeo = { linear/r+(angular*b/r), linear/r-(angular*b/r)};
                Object[] driveActive = { true, true };
                Object[] quickStop = { false, false };
                Object[] disableBrake = { true, true };
                /*
                foreach (var item in bridgeConfig.getPublicationList())
                {
                    this.PublishTwistMsg(item, lin, ang);
                }
                 * */
                foreach (var item in publicationList)
                {
                    this.PublishNeobotixCommandMsg(item, linNeo, 
                        driveActive, quickStop, disableBrake);
                }

            }
            catch (System.Net.Sockets.SocketException)
            {
                throw new System.Net.Sockets.SocketException(10);
            }
        }

        public void PublishturtleMessage(double linear, double angular, IList<String> publicationList)
        {
            try
            {
                var v = new { linear = linear, angular = angular };
                Object[] lin = { linear, 0.0, 0.0 };
                Object[] ang = { 0.0, 0.0, angular };
                /*
                foreach (var item in bridgeConfig.getPublicationList())
                {
                    this.PublishTwistMsg(item, lin, ang);
                }
                 * */
                foreach (var item in publicationList)
                {
                    this.PublishTwistMsg(item, lin, ang);
                }

            }
            catch (System.Net.Sockets.SocketException)
            {
                throw new System.Net.Sockets.SocketException(10);
            }
        }

        public RotRPY convertQuaternionToEuler(double x, double y, double z, double w)
        {
            RotRPY rot = new RotRPY();
            rot.roll = Math.Atan2(2 * (w * x + y * z), 1 - 2 * (Math.Pow(x, 2) + Math.Pow(y, 2)));
            rot.pitch = Math.Asin(2 * (w * y - z * x));
            rot.yaw = Math.Atan2(2 * (w * z + x * y), 1 - 2 * (Math.Pow(y, 2) + Math.Pow(z, 2)));
            return rot;
        }
    }

    public struct RotRPY
    {
        // Rotation about x axis
        public double roll;
        // Rotation about y axis
        public double pitch;
        // Rotation about z axis
        public double yaw;
    }
}
