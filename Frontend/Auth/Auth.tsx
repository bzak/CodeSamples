import * as React from "react"
let CSSTransitionGroup = (React as any).addons.CSSTransitionGroup;
import { locale, t, thtml } from "../i18n/translate"

export class Auth extends React.Component<{}, {}> {    
    render() {        
        return (
            <AuthLayout>
                <CSSTransitionGroup 
                    component="div"
                    transitionName="fadeIn" 
                    transitionEnterTimeout={500}
                    transitionLeave={false}>                    
                    {React.cloneElement((this.props as any).children, {
                        key: location.pathname
                    }) }
                </CSSTransitionGroup>
            </AuthLayout>
        )
    }
}


class AuthLayout extends React.Component<any, {}> {
    render() {        
        var hostParts = window.location.host.split(".");
        hostParts[0] = "www";
        var wwwHost = "https://"+hostParts.join(".");
        return (
            <div className="auth">
                <div className="container ">
                    <div className="row text-center">
                        <a href={wwwHost}>
                            <img src={"/content/" + locale + "/logo-wide.png"} alt="logo" className="lone-logo img-responsive" />
                        </a>

                        <div className="account-wall panel panel-default">
                            <div className="panel-body">
                                { this.props.children }
                            </div>
                        </div>
                    </div>
                </div>                
                <div className="sticky-footer">
                    <div className="">
                        <div className="">
                            <div className="pull-left">
                                {thtml("Auth:About")}
                                {thtml("Auth:Privacy")}
                                {thtml("Auth:Terms")}
                            </div>
                            <div className="pull-right">
                                {t("Auth:Copyright")}                                
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        )
    }
}