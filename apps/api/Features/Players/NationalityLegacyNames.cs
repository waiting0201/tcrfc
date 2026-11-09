namespace Tcrfc.Api.Features.Players;

/// <summary>
/// 舊資料（自由文字國名）→ 國籍代碼對照，僅供遷移 <c>20261109100000_PlayerNationalityToCode</c> 與其測試使用。
/// 遷移的 SQL 內嵌同一份清單（<c>db/migrations/20261109_player-nationality-code.sql</c>）；
/// <c>NationalityCodeTests</c> 會檢查兩邊一致。格式：代碼|別名,別名…（中英文國名皆收，比對不分大小寫）。
/// </summary>
public static class NationalityLegacyNames
{
    public static readonly string[] Lines =
    [
        "TW|台灣,臺灣,中華台北,中華民國,Taiwan,Chinese Taipei,Republic of China,ROC",
        "JP|日本,Japan",
        "KR|韓國,南韓,大韓民國,South Korea,Korea Republic,Republic of Korea,Korea",
        "KP|北韓,朝鮮,North Korea",
        "CN|中國,中國大陸,中華人民共和國,China,PR China",
        "HK|香港,Hong Kong",
        "MO|澳門,Macau,Macao",
        "US|美國,美利堅合眾國,United States,USA,United States of America",
        "CA|加拿大,Canada",
        "MX|墨西哥,Mexico",
        "BR|巴西,Brazil",
        "AR|阿根廷,Argentina",
        "CL|智利,Chile",
        "CO|哥倫比亞,Colombia",
        "PE|秘魯,Peru",
        "UY|烏拉圭,Uruguay",
        "PY|巴拉圭,Paraguay",
        "VE|委內瑞拉,Venezuela",
        "EC|厄瓜多,Ecuador",
        "BO|玻利維亞,Bolivia",
        "CR|哥斯大黎加,Costa Rica",
        "PA|巴拿馬,Panama",
        "JM|牙買加,Jamaica",
        "GB-ENG|英格蘭,England",
        "GB-SCT|蘇格蘭,Scotland",
        "GB-WLS|威爾斯,Wales",
        "GB-NIR|北愛爾蘭,Northern Ireland",
        "GB|英國,大不列顛,United Kingdom,UK,Great Britain",
        "IE|愛爾蘭,Ireland",
        "DE|德國,Germany",
        "FR|法國,France",
        "ES|西班牙,Spain",
        "PT|葡萄牙,Portugal",
        "IT|義大利,意大利,Italy",
        "NL|荷蘭,Netherlands,Holland",
        "BE|比利時,Belgium",
        "CH|瑞士,Switzerland",
        "AT|奧地利,Austria",
        "CZ|捷克,Czech Republic,Czechia",
        "SK|斯洛伐克,Slovakia",
        "PL|波蘭,Poland",
        "HU|匈牙利,Hungary",
        "RO|羅馬尼亞,Romania",
        "BG|保加利亞,Bulgaria",
        "GR|希臘,Greece",
        "TR|土耳其,Turkey,Türkiye",
        "RU|俄羅斯,Russia",
        "UA|烏克蘭,Ukraine",
        "BY|白俄羅斯,Belarus",
        "RS|塞爾維亞,Serbia",
        "HR|克羅埃西亞,克羅地亞,Croatia",
        "SI|斯洛維尼亞,Slovenia",
        "BA|波士尼亞,波士尼亞與赫塞哥維納,Bosnia and Herzegovina,Bosnia",
        "ME|蒙特內哥羅,蒙特尼哥羅,Montenegro",
        "MK|北馬其頓,馬其頓,North Macedonia,Macedonia",
        "AL|阿爾巴尼亞,Albania",
        "DK|丹麥,Denmark",
        "SE|瑞典,Sweden",
        "NO|挪威,Norway",
        "FI|芬蘭,Finland",
        "IS|冰島,Iceland",
        "EE|愛沙尼亞,Estonia",
        "LV|拉脫維亞,Latvia",
        "LT|立陶宛,Lithuania",
        "AU|澳洲,澳大利亞,Australia",
        "NZ|紐西蘭,New Zealand",
        "FJ|斐濟,Fiji",
        "PH|菲律賓,Philippines",
        "TH|泰國,Thailand",
        "VN|越南,Vietnam,Viet Nam",
        "ID|印尼,印度尼西亞,Indonesia",
        "MY|馬來西亞,Malaysia",
        "SG|新加坡,Singapore",
        "MM|緬甸,Myanmar,Burma",
        "KH|柬埔寨,Cambodia",
        "LA|寮國,Laos",
        "IN|印度,India",
        "PK|巴基斯坦,Pakistan",
        "BD|孟加拉,Bangladesh",
        "NP|尼泊爾,Nepal",
        "LK|斯里蘭卡,Sri Lanka",
        "IR|伊朗,Iran",
        "IQ|伊拉克,Iraq",
        "SA|沙烏地阿拉伯,沙烏地,Saudi Arabia",
        "AE|阿拉伯聯合大公國,阿聯,United Arab Emirates,UAE",
        "QA|卡達,Qatar",
        "IL|以色列,Israel",
        "JO|約旦,Jordan",
        "LB|黎巴嫩,Lebanon",
        "UZ|烏茲別克,Uzbekistan",
        "KZ|哈薩克,Kazakhstan",
        "NG|奈及利亞,Nigeria",
        "GH|迦納,Ghana",
        "CI|象牙海岸,科特迪瓦,Ivory Coast,Cote d'Ivoire",
        "SN|塞內加爾,Senegal",
        "CM|喀麥隆,Cameroon",
        "MA|摩洛哥,Morocco",
        "DZ|阿爾及利亞,Algeria",
        "TN|突尼西亞,Tunisia",
        "EG|埃及,Egypt",
        "ZA|南非,South Africa",
        "KE|肯亞,Kenya",
        "CD|剛果民主共和國,DR Congo",
        "ML|馬利,Mali",
        "GM|甘比亞,Gambia",
        "ZM|尚比亞,Zambia",
    ];

    /// <summary>別名（小寫）→ 代碼。</summary>
    public static IReadOnlyDictionary<string, string> Build()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in Lines)
        {
            var parts = line.Split('|');
            foreach (var alias in parts[1].Split(','))
                map[alias.Trim()] = parts[0];
        }
        return map;
    }
}
