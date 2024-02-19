using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Prost
{
    public partial class ProstForm
    {
        bool KiwoomErrorCatch(int code)
        {
            StringBuilder sb = new StringBuilder();
            code = code < 0 ? code * -1 : code;

            if (code != 0 && code != 1)
            {
                sb.Append("Error Code: ");
                sb.Append(code);
                sb.Append("\n");
                sb.Append("Trace: \n");
                sb.Append(GetStackTrade());
                sb.Append("Error Msg: \n");
            }

            switch (code)
            {
                case 1:         // 정상처리                                                                    
                    return true;
                case 0:         // 정상처리                                                                    
                    return true;
                case 10:        // 실패
                    sb.Append("실패");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 11:        // 조건번호 없슴                                                                
                    sb.Append("조건번호 없슴");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 12:        // 조건번호와 조건식 불일치                                                     
                    sb.Append("조건번호와 조건식 불일치");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 13:        // 조건검색 조회요청 초과                                                       
                    sb.Append("조건검색 조회요청 초과");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 100:        // 사용자정보교환 실패                                                           
                    sb.Append("사용자정보교환 실패");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 101:        // 서버 접속 실패                                                                
                    sb.Append("서버 접속 실패");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 102:        // 버전처리 실패                                                                 
                    sb.Append("버전처리 실패");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 103:        // 개인방화벽 실패                                                               
                    sb.Append("개인방화벽 실패");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 104:        // 메모리 보호실패                                                               
                    sb.Append("메모리 보호실패");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 105:        // 함수입력값 오류                                                               
                    sb.Append("함수입력값 오류");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 106:        // 통신연결 종료                                                                 
                    sb.Append("통신연결 종료");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 107:        // 보안모듈 오류                                                                 
                    sb.Append("보안모듈 오류");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 108:        // 공인인증 로그인 필요                                                          
                    sb.Append("공인인증 로그인 필요");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 200:        // 시세조회 과부하                                                               
                    sb.Append("시세조회 과부하");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 201:        // 전문작성 초기화 실패.                                                         
                    sb.Append("전문작성 초기화 실패.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 202:        // 전문작성 입력값 오류.                                                         
                    sb.Append("전문작성 입력값 오류.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 203:        // 데이터 없음.                                                                  
                    sb.Append("데이터 없음.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 204:        // 조회가능한 종목수 초과. 한번에 조회 가능한 종목개수는 최대 100종목.           
                    sb.Append("조회가능한 종목수 초과. 한번에 조회 가능한 종목개수는 최대 100종목.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 205:        // 데이터 수신 실패                                                              
                    sb.Append("// 데이터 수신 실패");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 206:        // 조회가능한 FID수 초과. 한번에 조회 가능한 FID개수는 최대 100개.               
                    sb.Append("// 조회가능한 FID수 초과. 한번에 조회 가능한 FID개수는 최대 100개.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 207:        // 실시간 해제오류                                                               
                    sb.Append("// 실시간 해제오류");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 209:        // 시세조회제한                                                               
                    sb.Append("// 시세조회제한");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 300:        // 입력값 오류                                                                   
                    sb.Append("// 입력값 오류");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 301:        // 계좌비밀번호 없음.                                                            
                    sb.Append("계좌비밀번호 없음.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 302:        // 타인계좌 사용오류.                                                            
                    sb.Append("타인계좌 사용오류.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 303:        // 주문가격이 주문착오 금액기준 초과.                                                     
                    sb.Append("주문가격이 주문착오 금액기준 초과.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 304:        // 주문가격이 주문착오 금액기준 초과.                                                     
                    sb.Append("주문가격이 주문착오 금액기준 초과.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 305:        // 주문수량이 총발행주수의 1% 초과오류.                                          
                    sb.Append("주문수량이 총발행주수의 1% 초과오류.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 306:        // 주문수량은 총발행주수의 3% 초과오류.                                          
                    sb.Append("주문수량은 총발행주수의 3% 초과오류.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 307:        // 주문전송 실패                                                                 
                    sb.Append("주문전송 실패");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 308:        // 주문전송 과부하                                                               
                    sb.Append("주문전송 과부하");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 309:        // 주문수량 300계약 초과.                                                        
                    sb.Append("주문수량 300계약 초과.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 310:        // 주문수량 500계약 초과.                                                        
                    sb.Append("주문수량 500계약 초과.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 311:        // 주문전송제한 과부하
                    sb.Append("주문전송제한 과부하");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 340:        // 계좌정보 없음.                                                                
                    sb.Append("계좌정보 없음.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
                case 500:        // 종목코드 없음.
                    sb.Append("종목코드 없음.");
                    StockLog.Logger.LOG.WriteLog("Error", "[KiwoomErrorCatch]: " + sb.ToString());
                    break;
            }
            return false;
        }

        string GetStackTrade() 
        {
            StringBuilder sb = new StringBuilder();

            int count = new StackTrace().FrameCount;
            StackTrace stacktrace = new StackTrace();
            MethodBase method = null;

            for (int i = 0; i < count; i++)
            {
                method = stacktrace.GetFrame(i).GetMethod();
                sb.Append(i);
                sb.Append(": ");
                sb.Append(method.Name);
                sb.Append("\n");
            }

            return sb.ToString();
        }
    }
}
