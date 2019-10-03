import * as React from "react"
import { t, thtml } from "../i18n/translate"
import { connectedForm } from "../Commons/forms"
import { StoreAuth, TokenResponseModel } from "../Api/api"
import { loginWithToken, setRedirectUri } from "./Actions"
import { showEmailForm } from "./LoginForm"

const boundState = (state) => state.auth;
const boundProps = [];
const boundActions = { login, showEmailForm };

interface ExternalProps {
    location: HistoryModule.Location;    
    login: (networkId: string, token: string) => void;
    showEmailForm: () => void;
    //loginWithToken: (token: TokenResponseModel) => void;
    //setRedirectUri: (uri: string) => void;
}
export default connectedForm(boundState, boundProps, boundActions)(
    class External extends React.Component<ExternalProps, {}> {
        getNetworkId() {
            return this.props.location.query["networkId"];
        }
        getToken() {
            return decodeURIComponent(this.props.location.hash.substring("#=token".length));
        }        
        getError() {
            return this.props.location.query["error"];
        }
        componentDidMount() {
            if (!this.getError()) {
                this.props.login(this.getNetworkId(), this.getToken());
            }
        }
        next() {
            if (this.props.location.query["next"]) {
                location.href = this.props.location.query["next"];
            } else {
                this.props.showEmailForm();
            }
        }
        render() {
            if (!this.getError()) {
                return (
                    <div>
                        <h1 className="login-title">
                            {t("Auth:Logging in")}...
                    </h1>
                    </div>
                );
            }
            return (
                <div className="form-signin">
                    <h1 className="login-title">
                        {t("Auth:Error logging in")}                        
                    </h1>
                    <p>{thtml("Auth:" + this.getError())}</p>
                    <p>&nbsp;</p>
                    <a className="btn btn-lg btn-primary btn-block" onClick={e => this.next()} > {t("Auth:Next")}</a>                    
                </div>
            )
        }
    }
)

function login(networkId: string, token: string) {
    return dispatch => {
        dispatch(setRedirectUri("/?networkId=" + networkId));
        dispatch(loginWithToken({
            access_token: token
        } as any));
    }
}