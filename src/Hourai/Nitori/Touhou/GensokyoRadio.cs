using System;
using System.Xml.Serialization;
using System.Collections.Generic;

namespace Hourai.Nitori.GensokyoRadio {

  [XmlRoot(ElementName="BITRATE")]
  public class Bitrate {
    [XmlElement(ElementName="BITRATE_1")]
    public string Bitrate1 { get; set; }
  }

  [XmlRoot(ElementName="SERVERINFO")]
  public class ServerInfo {
    [XmlElement(ElementName="LASTUPDATE")]
    public string LastUpdate { get; set; }
    [XmlElement(ElementName="SERVERS")]
    public string Servers { get; set; }
    [XmlElement(ElementName="STATUS")]
    public string Status { get; set; }
    [XmlElement(ElementName="LISTENERS")]
    public string Listeners { get; set; }
    [XmlElement(ElementName="BITRATE")]
    public Bitrate Bitrate { get; set; }
    [XmlElement(ElementName="MODE")]
    public string Mode { get; set; }
    [XmlElement(ElementName="AIMS")]
    public string Aims { get; set; }
  }

  [XmlRoot(ElementName="SONGINFO")]
  public class SongInfo {
      [XmlElement(ElementName="TITLE")]
      public string Title { get; set; }
      [XmlElement(ElementName="ARTIST")]
      public string Artist { get; set; }
      [XmlElement(ElementName="ALBUM")]
      public string Album { get; set; }
      [XmlElement(ElementName="YEAR")]
      public string Year { get; set; }
      [XmlElement(ElementName="CIRCLE")]
      public string Circle { get; set; }
  }

  [XmlRoot(ElementName="SONGTIMES")]
  public class SongTimes {
      [XmlElement(ElementName="DURATION")]
      public string Duration { get; set; }
      [XmlElement(ElementName="PLAYED")]
      public string Played { get; set; }
      [XmlElement(ElementName="REMAINING")]
      public string Remaining { get; set; }
      [XmlElement(ElementName="SONGSTART")]
      public string SongStart { get; set; }
      [XmlElement(ElementName="SONGEND")]
      public string SongEnd { get; set; }
  }

  [XmlRoot(ElementName="MISC")]
  public class Misc {
      [XmlElement(ElementName = "SONGID")]
      public string SongId { get; set; }
      [XmlElement(ElementName = "ALBUMID")]
      public string AlbumId { get; set; }
      [XmlElement(ElementName="IDCERTAINTY")]
      public string IdCertainty { get; set; }
      [XmlElement(ElementName="CIRCLELINK")]
      public string CircleLink{ get; set; }
      [XmlElement(ElementName="ALBUMART")]
      public string AlbumArt { get; set; }
      [XmlElement(ElementName="CIRCLEART")]
      public string CircleArt { get; set; }
      [XmlElement(ElementName="RATING")]
      public string Rating { get; set; }
      [XmlElement(ElementName="TIMESRATED")]
      public string TimesRated { get; set; }
      [XmlElement(ElementName="FORCEDELAY")]
      public string ForceDelay { get; set; }
  }

  [XmlRoot(ElementName="GENSOKYORADIODATA")]
  public class GensokyoRadioData {
      [XmlElement(ElementName="SERVERINFO")]
      public ServerInfo ServerInfo { get; set; }
      [XmlElement(ElementName="SONGINFO")]
      public SongInfo SongInfo { get; set; }
      [XmlElement(ElementName="SONGTIMES")]
      public SongTimes SongTimes { get; set; }
      [XmlElement(ElementName="MISC")]
      public Misc Misc{ get; set; }
  }

}
