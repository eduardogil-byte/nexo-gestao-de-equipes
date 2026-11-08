import {createContext,useCallback,useContext,useEffect,useMemo,useState,type ReactNode} from 'react';
import {api,setupApi} from '../lib/api';
import type {User} from '../lib/types';
const KEY='nexo-session';
interface Session { token:string; user:User }
interface AuthApi {session:Session|null;ready:boolean;login:(email:string,password:string)=>Promise<void>;register:(fullName:string,email:string,password:string,specialty:string)=>Promise<void>;logout:()=>void;refresh:()=>Promise<void>;isAdmin:boolean}
const Context=createContext<AuthApi|null>(null);
function storedSession():Session|null{try{const data=sessionStorage.getItem(KEY);return data?JSON.parse(data) as Session:null;}catch{return null;}}
export function AuthProvider({children}:{children:ReactNode}){
 const [session,setSession]=useState<Session|null>(storedSession);
 const [ready,setReady]=useState(false);
 const logout=useCallback(()=>{sessionStorage.removeItem(KEY);setSession(null);},[]);
 const save=useCallback((next:Session|null)=>{if(next)sessionStorage.setItem(KEY,JSON.stringify(next));else sessionStorage.removeItem(KEY);setSession(next);},[]);
 const refresh=useCallback(async()=>{const user=await api<User>('/auth/me');setSession(prev=>{if(!prev)return null;const next={...prev,user};sessionStorage.setItem(KEY,JSON.stringify(next));return next;});},[]);
 useEffect(()=>{setupApi(()=>storedSession()?.token||null,logout); const previous=storedSession();if(previous)api<User>('/auth/me').then(user=>save({...previous,user})).catch(logout).finally(()=>setReady(true));else setReady(true);},[logout,save]);
 const login=useCallback(async(email:string,password:string)=>{const res=await api<{accessToken:string;user:User}>('/auth/login',{method:'POST',body:JSON.stringify({email,password}),headers:{'Content-Type':'application/json'}});save({token:res.accessToken,user:res.user});},[save]);
 const register=useCallback(async(fullName:string,email:string,password:string,specialty:string)=>{await api('/auth/register',{method:'POST',body:JSON.stringify({fullName,email,password,specialty}),headers:{'Content-Type':'application/json'}});},[]);
 const value=useMemo(()=>({session,ready,login,register,logout,refresh,isAdmin:!!session?.user.roles?.includes('Admin')}),[session,ready,login,register,logout,refresh]);
 return <Context.Provider value={value}>{children}</Context.Provider>;
}
export function useAuth(){const auth=useContext(Context);if(!auth)throw Error('AuthProvider ausente');return auth;}
