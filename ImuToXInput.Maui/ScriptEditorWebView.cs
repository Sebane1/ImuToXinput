using System.Text;

namespace ImuToXInput.Maui;

/// <summary>
/// Builds HTML for an in-app script editor with offline syntax highlighting.
/// Textarea (transparent text) over a mirror div that shows the same text with colored spans.
/// No external scripts or CDN.
/// </summary>
public static class ScriptEditorWebView
{
    /// <summary>Returns full HTML: mirror div + textarea overlay, inline highlighter JS.</summary>
    public static string BuildHtml(string initialContent)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(initialContent ?? "");
        var forScript = json.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("<", "\\u003c");
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
        sb.Append("<style>*{box-sizing:border-box}html,body{margin:0;padding:0;height:100%;overflow:hidden}");
        sb.Append(".wrap{overflow:auto;width:100%;height:100%;min-height:240px;position:relative}");
        sb.Append("#mirror{display:block;width:100%;padding:8px;font-family:Consolas,monospace;font-size:13px;line-height:1.4;white-space:pre-wrap;word-wrap:break-word;color:#6a737d;min-height:100%;box-sizing:border-box}");
        sb.Append("#script{position:absolute;top:0;left:0;right:0;margin:0;padding:8px;font-family:Consolas,monospace;font-size:13px;line-height:1.4;background:transparent;color:transparent;caret-color:#fff;border:none;resize:none;outline:none;white-space:pre-wrap;overflow:hidden;box-sizing:border-box}");
        sb.Append(".c{color:#242}.s{color:#d73a49}.k{color:#032f62}.n{color:#005cc5}</style>");
        sb.Append("</head><body><div class=\"wrap\"><div id=\"mirror\"></div><textarea id=\"script\" placeholder=\"Script (axis/button/trigger)\"></textarea></div>");
        sb.Append("<script>");
        sb.Append("var ta=document.getElementById('script'),mirror=document.getElementById('mirror');");
        sb.Append("function esc(s){return s.replace(/&/g,'&amp;').replace(/\\x3c/g,'&lt;').replace(/\\x3e/g,'&gt;');}");
        sb.Append("function highlight(t){var used=Array(t.length);used.fill(false);var out=[];");
        sb.Append("var i=0;while(i<t.length){var rest=t.slice(i);");
        sb.Append("if(rest.match(/^\\/\\//)){var m=rest.match(/^\\/\\/[^\\n]*/);var len=m[0].length;for(var j=0;j<len;j++)used[i+j]=true;out.push({s:i,l:len,t:'c'});i+=len;continue;}");
        sb.Append("if(rest[0]==='\"'){var end=i+1;while(end<t.length){if(t[end]==='\\\\'&&end+1<t.length){end+=2;continue;}if(t[end]==='\"'){end++;break;}end++;}var len=end-i;for(var j=0;j<len;j++)used[i+j]=true;out.push({s:i,l:len,t:'s'});i=end;continue;}");
        sb.Append("var kw=/^(axis|button|trigger|name|trackers|processNames|when)\\b/.exec(rest);if(kw){var len=kw[0].length;var atLineStart=i===0||/\\n/.test(t[i-1]);var anyUsed=false;for(var j=0;j<len;j++)if(used[i+j])anyUsed=true;if(!anyUsed&&(atLineStart||kw[1]==='when')){for(var j=0;j<len;j++)used[i+j]=true;out.push({s:i,l:len,t:'k'});}i+=len;continue;}");
        sb.Append("var num=/^\\d+\\.?\\d*/.exec(rest);if(num){var len=num[0].length;var anyUsed=false;for(var j=0;j<len;j++)if(used[i+j])anyUsed=true;if(!anyUsed){for(var j=0;j<len;j++)used[i+j]=true;out.push({s:i,l:len,t:'n'});}i+=len;continue;}");
        sb.Append("i++;}");
        sb.Append("out.sort(function(a,b){return a.s-b.s;});var html='',pos=0;for(var x=0;x<out.length;x++){var o=out[x];if(o.s>pos)html+=esc(t.slice(pos,o.s));html+='<span class=\"'+o.t+'\">'+esc(t.slice(o.s,o.s+o.l))+'</span>';pos=o.s+o.l;}if(pos<t.length)html+=esc(t.slice(pos));return html.replace(/\\n/g,'<br>');}");
        sb.Append("function sync(){mirror.innerHTML=highlight(ta.value);ta.style.height=mirror.offsetHeight+'px';ta.style.width=mirror.offsetWidth+'px';}");
        sb.Append("ta.value=JSON.parse(\"").Append(forScript).Append("\")||'';sync();ta.oninput=function(){sync();};");
        sb.Append("</script></body></html>");
        return sb.ToString();
    }

    /// <summary>JS that returns the textarea content as base64 (avoids bridge issues with newlines/quotes).</summary>
    public const string GetContentScript = "(function(){var el=document.getElementById('script');var v=el?el.value:'';try{return btoa(unescape(encodeURIComponent(v)));}catch(e){return '';}})()";

    /// <summary>Builds JS to set textarea content. Pass a JSON-serialized string.</summary>
    public static string SetContentScript(string jsonEncodedValue)
    {
        return $"var e=document.getElementById('script');if(e){{e.value=JSON.parse({jsonEncodedValue});var m=document.getElementById('mirror');if(m&&e.oninput)e.oninput();}}";
    }
}
