"""Opt-in, isolated native Jellyfin 10.11.11 playback/FFmpeg verification. Python standard library only."""
from pathlib import Path
import argparse
import datetime
import json
import re
import secrets
import shutil
import socket
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parent.parent
PLUGIN_ID = 'c48a5098-04e0-4de5-a46c-8adae788f91b'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--port', type=int, default=18967)
    args = parser.parse_args()
    server = ROOT / '.local/server/jellyfin'
    plugin = ROOT / 'src/Jellyfin.Plugin.AudioTranscodingControl/bin/Release/net9.0/Jellyfin.Plugin.AudioTranscodingControl.dll'
    if not (server / 'jellyfin.exe').is_file() or not plugin.is_file():
        raise RuntimeError('Prepare the portable server and build Release first.')
    with socket.socket() as check:
        check.bind(('127.0.0.1', args.port))
    run = ROOT / '.local' / ('integration-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8])
    for child in ('data', 'config', 'cache', 'logs', 'media'):
        (run / child).mkdir(parents=True)
    (run / 'config/network.xml').write_text(f'''<?xml version="1.0"?>
<NetworkConfiguration><InternalHttpPort>{args.port}</InternalHttpPort><PublicHttpPort>{args.port}</PublicHttpPort>
<EnableIPv4>true</EnableIPv4><EnableIPv6>false</EnableIPv6><AutoDiscovery>false</AutoDiscovery><EnableRemoteAccess>false</EnableRemoteAccess>
<LocalNetworkAddresses><string>127.0.0.1</string></LocalNetworkAddresses></NetworkConfiguration>''', encoding='utf-8')
    (run / 'config/logging.json').write_text(json.dumps({'Serilog': {'MinimumLevel': {'Default': 'Information', 'Override': {'Jellyfin.Plugin.AudioTranscodingControl': 'Debug'}}}}), encoding='utf-8')
    plugin_dir = run / 'data/plugins/AudioTranscodingControl_0.0.1.0'
    plugin_dir.mkdir(parents=True)
    shutil.copy2(plugin, plugin_dir / plugin.name)

    # All sources are newly generated test signals; no existing media are opened or changed.
    media = run / 'media/ATC Synthetic 4K.mkv'
    command = [str(server / 'ffmpeg.exe'), '-hide_banner', '-v', 'error',
               '-f', 'lavfi', '-i', 'color=c=black:s=3840x2160:r=24',
               '-f', 'lavfi', '-i', 'anullsrc=r=48000:cl=5.1',
               '-f', 'lavfi', '-i', 'anullsrc=r=48000:cl=stereo',
               '-map', '0:v', '-map', '1:a', '-map', '1:a', '-map', '2:a', '-t', '6',
               '-c:v', 'libx265', '-preset', 'ultrafast', '-x265-params', 'pools=2:frame-threads=1:log-level=error', '-pix_fmt', 'yuv420p10le',
               '-c:a:0', 'aac', '-b:a:0', '768000', '-c:a:1', 'ac3', '-b:a:1', '640000', '-c:a:2', 'aac', '-b:a:2', '128000', str(media)]
    print('Generating synthetic 4K HEVC / multichannel AAC / AC3 / stereo source...', flush=True)
    generated = subprocess.run(command, capture_output=True, timeout=90, creationflags=subprocess.CREATE_NO_WINDOW)
    (run / 'generate.stderr').write_bytes(generated.stderr)
    if generated.returncode:
        raise RuntimeError(f'Synthetic media generation failed; see {run.name}/generate.stderr.')

    base = f'http://127.0.0.1:{args.port}'
    device = 'atc-integration-device'
    authorization = f'MediaBrowser Client="ATC Integration", Device="Isolated", DeviceId="{device}", Version="0.0.1"'
    headers = {'Authorization': authorization}

    def request(path, body=None, method=None, raw=False, custom_headers=None):
        url = urllib.parse.urljoin(base + '/', path)
        data = None if body is None else json.dumps(body).encode()
        current = {**headers, **(custom_headers or {})}
        if data is not None:
            current['Content-Type'] = 'application/json'
        req = urllib.request.Request(url, data=data, headers=current, method=method)
        try:
            with urllib.request.urlopen(req, timeout=35) as result:
                content = result.read()
                return content if raw else json.loads(content) if content else None
        except urllib.error.HTTPError as error:
            # Do not print request headers, token URLs, response bodies or credentials.
            raise RuntimeError(f'HTTP {error.code} on {urllib.parse.urlsplit(path).path}') from None

    stdout = (run / 'server.stdout').open('wb')
    stderr = (run / 'server.stderr').open('wb')
    process = subprocess.Popen([str(server / 'jellyfin.exe'), '--datadir', str(run / 'data'), '--configdir', str(run / 'config'),
                                '--cachedir', str(run / 'cache'), '--logdir', str(run / 'logs'), '--ffmpeg', str(server / 'ffmpeg.exe'),
                                '--nowebclient', '--nonetchange'], stdout=stdout, stderr=stderr, creationflags=subprocess.CREATE_NO_WINDOW)
    report = {'serverVersion': '10.11.11', 'pluginId': PLUGIN_ID, 'port': args.port, 'syntheticMedia': True, 'checks': {}}
    try:
        deadline = time.monotonic() + 80
        while True:
            try:
                info = request('/System/Info/Public')
                if info and info.get('Version') == '10.11.11':
                    break
            except (OSError, RuntimeError):
                pass
            if process.poll() is not None or time.monotonic() > deadline:
                raise RuntimeError(f'Isolated server did not become ready; see {run.name}/server.stdout.')
            time.sleep(1)
        print('Native server ready; configuring disposable test account and library...', flush=True)
        request('/Startup/User')
        password = secrets.token_urlsafe(24)
        request('/Startup/User', {'Name': 'atc-test', 'Password': password})
        request('/Startup/RemoteAccess', {'EnableRemoteAccess': False})
        request('/Startup/Complete', {}, method='POST')
        login = request('/Users/AuthenticateByName', {'Username': 'atc-test', 'Pw': password})
        user_id = login['User']['Id']
        headers['X-Emby-Token'] = login['AccessToken']
        del password, login
        plugins = request('/Plugins')
        loaded = next((p for p in plugins if p.get('Id', '').replace('-', '') == PLUGIN_ID.replace('-', '')), None)
        if not loaded or loaded.get('Status') not in ('Active', 'Restart'):
            raise RuntimeError('Plugin was not loaded by the native plugin manager.')
        report['checks']['nativePluginLoader'] = loaded['Status']

        def configure(enabled):
            request(f'/Plugins/{PLUGIN_ID}/Configuration', {'IsEnabled': enabled, 'EnableDebugLogging': True,
                    'Ac3Rule': {'IsEnabled': True, 'DeviceId': device, 'UserId': user_id}})

        configure(False)
        options = {'LibraryOptions': {'PathInfos': [{'Path': str(run / 'media')}], 'EnableRealtimeMonitor': False,
                   'SaveLocalMetadata': False, 'EnableInternetProviders': False,
                   'MetadataOptions': [{'ItemType': kind, 'MetadataFetchers': [], 'ImageFetchers': []} for kind in ('Video', 'Movie', 'Folder')]}}
        request('/Library/VirtualFolders?name=ATC&collectionType=homevideos&refreshLibrary=true', options)
        deadline = time.monotonic() + 60
        while True:
            items = request('/Items?' + urllib.parse.urlencode({'UserId': user_id, 'Recursive': 'true', 'IncludeItemTypes': 'Video,Movie', 'Fields': 'MediaSources'}))
            videos = [item for item in items.get('Items', []) if item.get('MediaSources')]
            if videos:
                item = videos[0]
                break
            if time.monotonic() > deadline:
                raise RuntimeError('Synthetic library scan did not complete.')
            time.sleep(1)
        item_id = item['Id']
        source = item['MediaSources'][0]
        profile = {'Name': 'AndroidTV-shaped-integration', 'MaxStreamingBitrate': 100000000, 'MaxStaticBitrate': 100000000,
                   'DirectPlayProfiles': [{'Type': 'Video', 'Container': 'mkv,mp4,ts', 'VideoCodec': 'hevc,h264', 'AudioCodec': 'aac,ac3,eac3,dts,truehd'}],
                   'TranscodingProfiles': [{'Type': 'Video', 'Context': 'Streaming', 'Container': container, 'Protocol': 'hls',
                                             'VideoCodec': 'hevc,h264', 'AudioCodec': 'aac,ac3,eac3,mp3'} for container in ('ts', 'mp4')],
                   'CodecProfiles': [], 'SubtitleProfiles': [], 'ContainerProfiles': []}
        # Exercise the first request for a newly scanned item, before any separate native probing call.
        configure(True)
        source = request(f'/Items/{item_id}/PlaybackInfo', {'MediaSourceId': source['Id'], 'AudioStreamIndex': 1, 'DeviceProfile': profile})['MediaSources'][0]
        report['checks']['firstPlaybackInfoAc3'] = 'AudioCodec=ac3' in source.get('TranscodingUrl', '')
        assert report['checks']['firstPlaybackInfoAc3']
        configure(False)
        stream_audio = [s for s in source['MediaStreams'] if s['Type'] == 'Audio']
        multi = next(s['Index'] for s in stream_audio if s['Codec'] == 'aac' and s['Channels'] == 6)
        ac3 = next(s['Index'] for s in stream_audio if s['Codec'] == 'ac3')
        stereo = next(s['Index'] for s in stream_audio if s['Codec'] == 'aac' and s['Channels'] == 2)

        def playback(index, **changes):
            body = {'DeviceProfile': profile, 'MediaSourceId': source['Id'], 'AudioStreamIndex': index, 'EnableDirectPlay': True,
                    'EnableDirectStream': True, 'EnableTranscoding': True, 'AllowVideoStreamCopy': True, 'AllowAudioStreamCopy': True, **changes}
            return request(f'/Items/{item_id}/PlaybackInfo', body)['MediaSources'][0]

        baseline = playback(multi)
        assert baseline['SupportsDirectPlay']
        configure(True)
        changed = playback(multi)
        assert not changed['SupportsDirectPlay']
        query = urllib.parse.parse_qs(urllib.parse.urlsplit(changed['TranscodingUrl']).query)
        assert query['AudioCodec'] == ['ac3']
        assert int(query['AudioBitrate'][0]) <= 640000
        assert query['TranscodingMaxAudioChannels'] == ['6']
        report['checks']['playbackInfoProfileInfluencesNativeDecision'] = True
        report['checks']['nativePlaybackParameters'] = {key: query.get(key) for key in ('AudioCodec', 'AudioBitrate', 'TranscodingMaxAudioChannels', 'VideoCodec', 'SegmentContainer')}
        print('PlaybackInfo selected AC3; fetching its HLS master, media playlist and segment...', flush=True)

        master_url = changed['TranscodingUrl']
        master = request(master_url, raw=True).decode()
        media_path = next(line for line in master.splitlines() if line and not line.startswith('#'))
        playlist_url = urllib.parse.urljoin(urllib.parse.urljoin(base, master_url), media_path)
        playlist = request(playlist_url, raw=True).decode()
        segment_path = next(line for line in playlist.splitlines() if line and not line.startswith('#'))
        segment_url = urllib.parse.urljoin(playlist_url, segment_path)
        segment = request(segment_url, raw=True)
        segment_file = run / 'segment.ts'
        segment_file.write_bytes(segment)
        probe = subprocess.run([str(server / 'ffprobe.exe'), '-v', 'error', '-show_entries', 'stream=codec_type,codec_name,channels,bit_rate,width,height', '-of', 'json', str(segment_file)], capture_output=True, timeout=20, creationflags=subprocess.CREATE_NO_WINDOW)
        assert probe.returncode == 0
        stream_info = json.loads(probe.stdout)['streams']
        audio_output = next(s for s in stream_info if s['codec_type'] == 'audio')
        video_output = next(s for s in stream_info if s['codec_type'] == 'video')
        assert audio_output['codec_name'] == 'ac3' and audio_output['channels'] == 6
        assert video_output['codec_name'] == 'hevc' and video_output['width'] == 3840 and video_output['height'] == 2160
        report['checks']['segmentProbe'] = {'audio': audio_output, 'video': video_output}
        logs = '\n'.join(p.read_text(encoding='utf-8-sig', errors='replace') for p in (run / 'logs').glob('*') if p.is_file())
        assert '-codec:v:0 copy' in logs and '-codec:a:0 ac3' in logs and '-ac 6' in logs and '-ab 640000' in logs
        report['checks']['nativeFfmpegArguments'] = '-codec:v:0 copy -codec:a:0 ac3 -ac 6 -ab 640000'
        report['checks']['ac3OriginalDirectPlay'] = playback(ac3)['SupportsDirectPlay']
        report['checks']['stereoDirectPlay'] = playback(stereo)['SupportsDirectPlay']
        report['checks']['repeatedPlaybackInfoAc3'] = 'AudioCodec=ac3' in playback(multi)['TranscodingUrl']
        assert report['checks']['ac3OriginalDirectPlay'] and report['checks']['stereoDirectPlay'] and report['checks']['repeatedPlaybackInfoAc3']
        report['checks']['explicitTranscodingFalseRetainsNativeBehavior'] = playback(multi, EnableTranscoding=False)['SupportsDirectPlay']
        assert report['checks']['explicitTranscodingFalseRetainsNativeBehavior']
        report['checks']['pluginDecisionLogged'] = 'ATC applied:' in logs
        assert report['checks']['pluginDecisionLogged']
        other_authorization = authorization.replace('DeviceId="atc-integration-device"', 'DeviceId="atc-other-device"')
        unmatched_body = {'DeviceProfile': profile, 'MediaSourceId': source['Id'], 'AudioStreamIndex': multi}
        unmatched = request(f'/Items/{item_id}/PlaybackInfo', unmatched_body, custom_headers={'Authorization': other_authorization})
        report['checks']['unmatchedDeviceNativeDirectPlay'] = unmatched['MediaSources'][0]['SupportsDirectPlay']
        assert report['checks']['unmatchedDeviceNativeDirectPlay']

        # Explicit requests to streaming actions bypass the PlaybackInfo hook. Verify and disclose that boundary.
        direct_path = f'/Videos/{item_id}/master.m3u8?' + urllib.parse.urlencode({
            'MediaSourceId': source['Id'], 'AudioStreamIndex': multi, 'AudioCodec': 'aac', 'VideoCodec': 'hevc,h264',
            'AudioBitrate': 640000, 'TranscodingMaxAudioChannels': 6, 'AllowAudioStreamCopy': 'false',
            'SegmentContainer': 'ts', 'PlaySessionId': uuid.uuid4().hex})
        direct_master = request(direct_path, raw=True).decode()
        direct_playlist_path = next(line for line in direct_master.splitlines() if line and not line.startswith('#'))
        direct_playlist_url = urllib.parse.urljoin(urllib.parse.urljoin(base, direct_path), direct_playlist_path)
        direct_playlist = request(direct_playlist_url, raw=True).decode()
        direct_segment_path = next(line for line in direct_playlist.splitlines() if line and not line.startswith('#'))
        direct_file = run / 'direct-aac.ts'
        direct_file.write_bytes(request(urllib.parse.urljoin(direct_playlist_url, direct_segment_path), raw=True))
        direct_probe = subprocess.run([str(server / 'ffprobe.exe'), '-v', 'error', '-select_streams', 'a:0', '-show_entries',
                                       'stream=codec_name,channels', '-of', 'json', str(direct_file)], capture_output=True, timeout=20, creationflags=subprocess.CREATE_NO_WINDOW)
        direct_audio = json.loads(direct_probe.stdout)['streams'][0]
        assert direct_audio['codec_name'] == 'aac'
        report['checks']['directStreamingBypassesPlaybackInfo'] = direct_audio
        report['limits'] = ['Synthetic request shaped like the client; no Android TV / Fire TV app was operated.',
                            'Synthetic 4K SDR HEVC signal; no HDR, Dolby Vision or Atmos hardware verification.',
                            'Direct streaming requests that bypass PlaybackInfo are not rewritten by this plugin.']
        report['outcome'] = 'native-server-playback-and-ffmpeg-verified'
        (run / 'report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        print(json.dumps(report, indent=2), flush=True)
        print('Sanitized evidence: ' + str(run.relative_to(ROOT) / 'report.json'), flush=True)
    finally:
        process.terminate()
        try:
            process.wait(timeout=12)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
        stdout.close()
        stderr.close()


if __name__ == '__main__':
    main()
